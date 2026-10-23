using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using PandaBear.Testing.Analyzers;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PandaBear.Testing.CodeFixes;

public sealed partial class SharedKernelTestingCodeFixProvider
{
    private const string UserMessageHelperName = "WithUserMessage";

    private const string UserMessageParameterDocumentation =
        "/// <param name=\"userMessage\">Why the test expects the assertion to hold; the failure message starts with it.</param>";

    private const string UserMessageNullCondition = "Also thrown when <paramref name=\"userMessage\"/> is null.";

    private static readonly string[] CrefPrefixes = ["T:", "global::", "System."];

    private static async Task RegisterUserMessageOverloadFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        SyntaxNode root)
    {
        var ct = context.CancellationToken;

        // The overload copies a declaration with a body and forwards each parameter by name, which __arglist does not have.
        if (root.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } declaration
            || (declaration.Body is null && declaration.ExpressionBody is null)
            || declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword) || modifier.IsKind(SyntaxKind.ExternKeyword))
            || declaration.ParameterList.Parameters.Any(static parameter => !parameter.Identifier.IsKind(SyntaxKind.IdentifierToken)))
        {
            return;
        }

        var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (model?.GetDeclaredSymbol(declaration, ct) is not IMethodSymbol assertion
            || !AssertionUserMessageOverload.IsMissing(assertion, model.Compilation, out var messageIndex, out var blocker)
            || blocker is not null)
        {
            return;
        }

        var updatedDocument = await AddUserMessageOverload(document, root, model, declaration, messageIndex, ct).ConfigureAwait(false);
        if (updatedDocument is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add userMessage overload",
                _ => Task.FromResult(updatedDocument),
                equivalenceKey: "AddUserMessageOverload"),
            diagnostic);
    }

    // Returns the document with the overload after the assertion, or null when the overload would not be a safe forwarder.
    private static async Task<Document?> AddUserMessageOverload(
        Document document,
        SyntaxNode root,
        SemanticModel model,
        MethodDeclarationSyntax declaration,
        int messageIndex,
        CancellationToken ct)
    {
        var assertionAnnotation = new SyntaxAnnotation();
        var forwardedCallAnnotation = new SyntaxAnnotation();
        var helperCallAnnotation = new SyntaxAnnotation();
        var overload = CreateUserMessageOverload(
            model,
            declaration,
            messageIndex,
            GetEndOfLine(root),
            forwardedCallAnnotation,
            helperCallAnnotation,
            ct);
        var annotatedRoot = root.ReplaceNode(declaration, declaration.WithAdditionalAnnotations(assertionAnnotation));
        var updatedRoot = annotatedRoot.InsertNodesAfter(annotatedRoot.GetAnnotatedNodes(assertionAnnotation).First(), [overload]);

        // Validate the overload as it is applied: dotnet format reports type arguments that inference supplies as IDE0001.
        var updatedDocument = await Simplifier.ReduceAsync(document.WithSyntaxRoot(updatedRoot), Simplifier.Annotation, cancellationToken: ct)
            .ConfigureAwait(false);

        return await IsSafeUserMessageOverload(document, updatedDocument, assertionAnnotation, forwardedCallAnnotation, helperCallAnnotation, ct)
            .ConfigureAwait(false)
            ? updatedDocument
            : null;
    }

    private static MethodDeclarationSyntax CreateUserMessageOverload(
        SemanticModel model,
        MethodDeclarationSyntax declaration,
        int messageIndex,
        string endOfLine,
        SyntaxAnnotation forwardedCallAnnotation,
        SyntaxAnnotation helperCallAnnotation,
        CancellationToken ct)
    {
        var parameters = declaration.ParameterList.Parameters;
        var indentation = GetIndentation(declaration);
        var overload = declaration
            .WithAttributeLists(List(declaration.AttributeLists.Select(list => WithoutSuppressions(model, list, ct)).OfType<AttributeListSyntax>()))
            .WithModifiers(TokenList(declaration.Modifiers.Where(static modifier => !modifier.IsKind(SyntaxKind.AsyncKeyword)
                && !modifier.IsKind(SyntaxKind.PartialKeyword)
                && !modifier.IsKind(SyntaxKind.ExternKeyword))))
            .WithParameterList(declaration.ParameterList.WithParameters(InsertUserMessageParameter(parameters, messageIndex)))
            .WithBody(null)
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        // The arrow ends the signature's last line, after the parameter list or the last constraint.
        var signatureEnd = overload.ConstraintClauses.Count > 0
            ? overload.ConstraintClauses.Last().GetLastToken()
            : overload.ParameterList.CloseParenToken;
        overload = overload.ReplaceToken(signatureEnd, signatureEnd.WithTrailingTrivia());

        var helperCall = CreateHelperCall(declaration, endOfLine, forwardedCallAnnotation)
            .WithLeadingTrivia(Whitespace(indentation + GetIndentationUnit(declaration, indentation)))
            .WithAdditionalAnnotations(helperCallAnnotation);

        return overload
            .WithExpressionBody(ArrowExpressionClause(
                Token(TriviaList(Space), SyntaxKind.EqualsGreaterThanToken, TriviaList(EndOfLine(endOfLine))),
                helperCall))
            .WithSemicolonToken(Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(EndOfLine(endOfLine))))
            .WithLeadingTrivia(CreateOverloadLeadingTrivia(model, declaration, messageIndex, indentation, endOfLine))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    // WithUserMessage(userMessage, () => receiver.Name<T…>(other parameters)), where the Simplifier later removes the type
    // arguments that inference supplies.
    private static InvocationExpressionSyntax CreateHelperCall(
        MethodDeclarationSyntax declaration,
        string endOfLine,
        SyntaxAnnotation forwardedCallAnnotation)
    {
        var parameters = declaration.ParameterList.Parameters;
        SimpleNameSyntax name = declaration.TypeParameterList is { } typeParameterList
            ? GenericName(
                    declaration.Identifier.WithoutTrivia(),
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        typeParameterList.Parameters.Select(static typeParameter => IdentifierName(typeParameter.Identifier.WithoutTrivia())))))
                .WithAdditionalAnnotations(Simplifier.Annotation)
            : IdentifierName(declaration.Identifier.WithoutTrivia());
        var forwardedCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(parameters[0].Identifier.WithoutTrivia()), name),
                ArgumentList(SeparatedList(parameters.Skip(1).Select(static parameter => Argument(IdentifierName(parameter.Identifier.WithoutTrivia()))))))
            .WithAdditionalAnnotations(forwardedCallAnnotation);

        return InvocationExpression(
                IdentifierName(UserMessageHelperName),
                ArgumentList(SeparatedList(
                [
                    Argument(IdentifierName(AssertionUserMessageOverload.ParameterName)),
                    Argument(ParenthesizedLambdaExpression(forwardedCall)),
                ])))
            .NormalizeWhitespace(eol: endOfLine);
    }

    private static AttributeListSyntax? WithoutSuppressions(SemanticModel model, AttributeListSyntax list, CancellationToken ct)
    {
        var kept = list.Attributes.Where(attribute => !IsSuppressionAttribute(model, attribute, ct)).ToList();
        return kept.Count == list.Attributes.Count ? list
            : kept.Count == 0 ? null
            : list.WithAttributes(SeparatedList(kept));
    }

    private static bool IsSuppressionAttribute(SemanticModel model, AttributeSyntax attribute, CancellationToken ct) =>
        model.GetSymbolInfo(attribute, ct).Symbol?.ContainingType is { Name: "SuppressMessageAttribute" or "UnconditionalSuppressMessageAttribute" } attributeType
        && string.Equals(attributeType.ContainingNamespace?.ToDisplayString(), "System.Diagnostics.CodeAnalysis", StringComparison.Ordinal);

    // Inserts string userMessage at the message index. Parameters before it lose params and default values. The new parameter
    // takes the line indentation of its neighbor and the layout of the comma before it without its comments, which keeps the
    // list's line breaks. When the close parenthesis has a line of its own, the new parameter gets one too.
    private static SeparatedSyntaxList<ParameterSyntax> InsertUserMessageParameter(SeparatedSyntaxList<ParameterSyntax> parameters, int messageIndex)
    {
        var nodes = parameters.Select((parameter, index) => index < messageIndex ? WithoutParamsOrDefault(parameter) : parameter).ToList();
        var separators = parameters.GetSeparators().ToList();
        var neighbor = nodes[messageIndex < nodes.Count ? messageIndex : nodes.Count - 1];
        var messageParameter = Parameter(Identifier(AssertionUserMessageOverload.ParameterName))
            .WithType(PredefinedType(Token(SyntaxKind.StringKeyword)).WithTrailingTrivia(Space));
        var lastLineEnd = messageIndex == nodes.Count ? neighbor.GetTrailingTrivia() : TriviaList();
        SyntaxToken separator;
        if (lastLineEnd.Any(SyntaxKind.EndOfLineTrivia))
        {
            // The comma takes over the end of the last parameter's line, comments included.
            nodes[nodes.Count - 1] = neighbor.WithoutTrailingTrivia();
            separator = Token(TriviaList(), SyntaxKind.CommaToken, lastLineEnd);
            messageParameter = messageParameter
                .WithLeadingTrivia(GetLineIndentation(neighbor.GetLeadingTrivia()))
                .WithTrailingTrivia(lastLineEnd.Last(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)));
        }
        else
        {
            separator = separators.Count > 0
                ? WithoutComments(separators[Math.Min(messageIndex, separators.Count) - 1])
                : Token(TriviaList(), SyntaxKind.CommaToken, TriviaList(Space));
            messageParameter = messageParameter.WithLeadingTrivia(parameters.Count > 1 ? GetLineIndentation(neighbor.GetLeadingTrivia()) : TriviaList());
        }

        nodes.Insert(messageIndex, messageParameter);
        separators.Insert(Math.Min(messageIndex, separators.Count), separator);

        return SeparatedList(nodes, separators);
    }

    // The whitespace that starts the line of a token with this leading trivia: after its last line break, else at its start.
    private static SyntaxTriviaList GetLineIndentation(SyntaxTriviaList leadingTrivia)
    {
        var lineStart = 0;
        for (var index = 0; index < leadingTrivia.Count; index++)
        {
            if (leadingTrivia[index].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                lineStart = index + 1;
            }
        }

        return lineStart < leadingTrivia.Count && leadingTrivia[lineStart].IsKind(SyntaxKind.WhitespaceTrivia)
            ? TriviaList(leadingTrivia[lineStart])
            : TriviaList();
    }

    // The token with the indentation of its line and its line break, or a space, but without comments.
    private static SyntaxToken WithoutComments(SyntaxToken token) =>
        token
            .WithLeadingTrivia(GetLineIndentation(token.LeadingTrivia))
            .WithTrailingTrivia(token.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia)
                ? TriviaList(token.TrailingTrivia.Last(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                : TriviaList(Space));

    private static ParameterSyntax WithoutParamsOrDefault(ParameterSyntax parameter)
    {
        if (parameter.Default is { } defaultValue)
        {
            parameter = parameter
                .WithDefault(null)
                .WithIdentifier(parameter.Identifier.WithTrailingTrivia(defaultValue.GetTrailingTrivia()));
        }

        var paramsIndex = parameter.Modifiers.IndexOf(SyntaxKind.ParamsKeyword);
        return paramsIndex < 0 || parameter.Type is not { } type
            ? parameter
            : parameter
                .WithModifiers(parameter.Modifiers.RemoveAt(paramsIndex))
                .WithType(type.WithLeadingTrivia(parameter.Modifiers[paramsIndex].LeadingTrivia));
    }

    // One blank line, a copy of the assertion's documentation with the userMessage exception condition and, when the assertion
    // documents its parameters, the userMessage parameter, and then the assertion's indentation. Other comments and directives
    // stay with the assertion.
    private static SyntaxTriviaList CreateOverloadLeadingTrivia(
        SemanticModel model,
        MethodDeclarationSyntax declaration,
        int messageIndex,
        string indentation,
        string endOfLine)
    {
        var documentation = string.Concat(GetDocumentationComments(declaration));
        if (documentation.Length > 0)
        {
            var parameterName = declaration.ParameterList.Parameters[messageIndex - 1].Identifier.ValueText;
            documentation = AddUserMessageDocumentation(documentation, parameterName, GetArgumentNullExceptionCref(model, declaration.SpanStart), endOfLine);
        }

        return ParseLeadingTrivia(endOfLine + documentation + indentation);
    }

    private static IEnumerable<string> GetDocumentationComments(MethodDeclarationSyntax declaration)
    {
        var trivia = declaration.GetLeadingTrivia();
        for (var index = 0; index < trivia.Count; index++)
        {
            if (trivia[index].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                var lineIndentation = index > 0 && trivia[index - 1].IsKind(SyntaxKind.WhitespaceTrivia)
                    ? trivia[index - 1].ToFullString()
                    : string.Empty;
                yield return lineIndentation + trivia[index].ToFullString();
            }
        }
    }

    private static string AddUserMessageDocumentation(string documentation, string precedingParameterName, string exceptionCref, string endOfLine)
    {
        if (!documentation.EndsWith("\n", StringComparison.Ordinal))
        {
            documentation += endOfLine;
        }

        // The parameter line follows the line that closes the anchor element, with that line's indentation and line break. A
        // comment that documents no parameter gets none, because CS1573 reports a comment that documents only some of them.
        var anchorEnd = FindUserMessageDocumentationAnchor(documentation, precedingParameterName);
        if (anchorEnd >= 0)
        {
            var insertAt = GetNextLineStart(documentation, anchorEnd);
            documentation = documentation.Insert(insertAt, CreateDocumentationLine(documentation, insertAt, UserMessageParameterDocumentation));
        }

        // Each exception type has one element, so an existing ArgumentNullException element gains the userMessage condition,
        // on a line of its own when its end tag starts a line.
        var endTagStart = FindArgumentNullExceptionEndTag(documentation);
        if (endTagStart >= 0)
        {
            var lineStart = documentation.LastIndexOf('\n', endTagStart - 1) + 1;
            return string.Equals(documentation.Substring(lineStart, endTagStart - lineStart).Trim(), "///", StringComparison.Ordinal)
                ? documentation.Insert(lineStart, CreateDocumentationLine(documentation, lineStart, "/// " + UserMessageNullCondition))
                : documentation.Insert(endTagStart, " " + UserMessageNullCondition);
        }

        var exception = "/// <exception cref=\"" + exceptionCref + "\">Thrown when <paramref name=\"userMessage\"/> is null.</exception>";
        return documentation + CreateDocumentationLine(documentation, documentation.Length, exception);
    }

    // The end of the param element of the parameter before userMessage, else of the last param element; -1 when the comment
    // documents no parameter.
    private static int FindUserMessageDocumentationAnchor(string documentation, string precedingParameterName)
    {
        var elements = GetDocumentationElements(documentation).ToList();
        var anchor = elements.LastOrDefault(element => IsXmlElement(element, "param", precedingParameterName))
            ?? elements.LastOrDefault(static element => IsXmlElement(element, "param", nameAttribute: null));

        return anchor?.Span.End ?? -1;
    }

    // The start of the end tag of the comment's ArgumentNullException element, or -1.
    private static int FindArgumentNullExceptionEndTag(string documentation) =>
        GetDocumentationElements(documentation)
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(static element => string.Equals(element.StartTag.Name.LocalName.ValueText, "exception", StringComparison.Ordinal)
                && element.StartTag.Attributes.Any(static attribute => attribute switch
                {
                    XmlCrefAttributeSyntax crefAttribute => NamesArgumentNullException(crefAttribute.Cref.ToString()),
                    XmlTextAttributeSyntax { Name.LocalName.ValueText: "cref" } textAttribute =>
                        NamesArgumentNullException(string.Concat(textAttribute.TextTokens.Select(static token => token.ValueText))),
                    _ => false,
                })) is { } element
            ? element.EndTag.SpanStart
            : -1;

    // Matches ArgumentNullException as a short name, as System.ArgumentNullException with or without global::, and as T: ID.
    private static bool NamesArgumentNullException(string cref)
    {
        var name = cref.Trim();
        foreach (var prefix in CrefPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                name = name.Substring(prefix.Length);
            }
        }

        return string.Equals(name, "ArgumentNullException", StringComparison.Ordinal);
    }

    private static IEnumerable<XmlNodeSyntax> GetDocumentationElements(string documentation) =>
        ParseLeadingTrivia(documentation)
            .Select(static trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .SelectMany(static comment => comment.Content);

    private static bool IsXmlElement(XmlNodeSyntax node, string name, string? nameAttribute) => node switch
    {
        XmlElementSyntax element => HasXmlName(element.StartTag.Name, element.StartTag.Attributes, name, nameAttribute),
        XmlEmptyElementSyntax emptyElement => HasXmlName(emptyElement.Name, emptyElement.Attributes, name, nameAttribute),
        _ => false,
    };

    private static bool HasXmlName(XmlNameSyntax elementName, SyntaxList<XmlAttributeSyntax> attributes, string name, string? nameAttribute) =>
        string.Equals(elementName.LocalName.ValueText, name, StringComparison.Ordinal)
        && (nameAttribute is null
            || attributes.OfType<XmlNameAttributeSyntax>().Any(attribute => string.Equals(attribute.Identifier.Identifier.ValueText, nameAttribute, StringComparison.Ordinal)));

    // A documentation line to insert at lineStart, with the indentation and line break of the line that ends there.
    private static string CreateDocumentationLine(string documentation, int lineStart, string content)
    {
        var previousLineEnd = lineStart - 1;
        var previousLineStart = documentation.LastIndexOf('\n', Math.Max(previousLineEnd - 1, 0)) + 1;
        var indentationLength = 0;
        while (previousLineStart + indentationLength < documentation.Length
            && documentation[previousLineStart + indentationLength] is ' ' or '\t')
        {
            indentationLength++;
        }

        var lineBreak = previousLineEnd > 0 && documentation[previousLineEnd - 1] == '\r' ? "\r\n" : "\n";
        return documentation.Substring(previousLineStart, indentationLength) + content + lineBreak;
    }

    private static int GetNextLineStart(string text, int position)
    {
        var lineBreak = text.IndexOf('\n', position);
        return lineBreak < 0 ? text.Length : lineBreak + 1;
    }

    private static string GetArgumentNullExceptionCref(SemanticModel model, int position)
    {
        var argumentNullException = model.Compilation.GetTypeByMetadataName("System.ArgumentNullException");
        var shortName = model.GetSpeculativeSymbolInfo(position, IdentifierName("ArgumentNullException"), SpeculativeBindingOption.BindAsTypeOrNamespace);
        return argumentNullException is not null && SymbolEqualityComparer.Default.Equals(shortName.Symbol, argumentNullException)
            ? "ArgumentNullException"
            : "System.ArgumentNullException";
    }

    private static string GetEndOfLine(SyntaxNode root) =>
        root.DescendantTrivia().FirstOrDefault(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)) is { RawKind: not 0 } endOfLine
            ? endOfLine.ToFullString()
            : "\n";

    private static string GetIndentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        return trivia.Count > 0 && trivia[trivia.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? trivia[trivia.Count - 1].ToFullString()
            : string.Empty;
    }

    // One indentation level is the difference between the member and its containing type.
    private static string GetIndentationUnit(MethodDeclarationSyntax declaration, string indentation)
    {
        var typeIndentation = declaration.Parent is { } containingType ? GetIndentation(containingType) : string.Empty;
        return indentation.Length > typeIndentation.Length && indentation.StartsWith(typeIndentation, StringComparison.Ordinal)
            ? indentation.Substring(typeIndentation.Length)
            : "    ";
    }

    // The fix registers only when the forwarded call binds to the assertion, the helper is a static method that takes the message
    // first and has the shape that awaits a task result, no extension GetAwaiter makes another result awaitable, the assertion
    // is covered afterwards, and the project gains no compiler warning or error.
    private static async Task<bool> IsSafeUserMessageOverload(
        Document document,
        Document updatedDocument,
        SyntaxAnnotation assertionAnnotation,
        SyntaxAnnotation forwardedCallAnnotation,
        SyntaxAnnotation helperCallAnnotation,
        CancellationToken ct)
    {
        var root = await updatedDocument.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var model = await updatedDocument.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root is null
            || model is null
            || root.GetAnnotatedNodes(assertionAnnotation).FirstOrDefault() is not MethodDeclarationSyntax assertionDeclaration
            || root.GetAnnotatedNodes(forwardedCallAnnotation).FirstOrDefault() is not InvocationExpressionSyntax forwardedCall
            || root.GetAnnotatedNodes(helperCallAnnotation).FirstOrDefault() is not InvocationExpressionSyntax helperCall
            || model.GetDeclaredSymbol(assertionDeclaration, ct) is not IMethodSymbol assertion
            || model.GetSymbolInfo(forwardedCall, ct).Symbol is not IMethodSymbol forwarded
            || !SymbolEqualityComparer.Default.Equals((forwarded.ReducedFrom ?? forwarded).OriginalDefinition, assertion)
            || model.GetSymbolInfo(helperCall, ct).Symbol is not IMethodSymbol { IsStatic: true } helper
            || helper.Parameters.Length == 0
            || helper.Parameters[0].Type.SpecialType != SpecialType.System_String
            || !AwaitsTaskResult(helper, assertion)
            || IsAwaitableThroughExtension(model, forwardedCall, forwarded)
            || AssertionUserMessageOverload.IsMissing(assertion, model.Compilation, out _, out _))
        {
            return false;
        }

        return !await AddsCompilerDiagnostics(document.Project, updatedDocument.Project, ct).ConfigureAwait(false);
    }

    // The analyzer blocks results with an instance GetAwaiter other than Task and Task<T>. A result that an extension
    // GetAwaiter makes awaitable binds the lambda to Func<T>, which returns it without awaiting it.
    private static bool IsAwaitableThroughExtension(SemanticModel model, InvocationExpressionSyntax forwardedCall, IMethodSymbol forwarded) =>
        !forwarded.ReturnsVoid
        && !IsTaskType(forwarded.ReturnType)
        && model.LookupSymbols(forwardedCall.SpanStart, forwarded.ReturnType, "GetAwaiter", includeReducedExtensionMethods: true)
            .Any(static symbol => symbol is IMethodSymbol { Parameters.IsEmpty: true });

    // A task-returning lambda also binds to a Func<T> helper, which returns the task without awaiting it, so a failure would
    // complete without the message. Such an assertion needs the helper shape whose delegate returns a task.
    private static bool AwaitsTaskResult(IMethodSymbol helper, IMethodSymbol assertion)
    {
        var helperParameters = helper.OriginalDefinition.Parameters;
        return !IsTaskType(assertion.ReturnType)
            || (helperParameters.Length > 1
                && helperParameters[1].Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
                && IsTaskType(invoke.ReturnType));
    }

    private static bool IsTaskType(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task", ContainingType: null } namedType
        && string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Threading.Tasks", StringComparison.Ordinal);

    // Compares unsuppressed compiler warnings and errors as a multiset of (ID, file, message), so a diagnostic that moves with
    // the inserted lines is not new.
    private static async Task<bool> AddsCompilerDiagnostics(Project project, Project updatedProject, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        var updatedCompilation = await updatedProject.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null || updatedCompilation is null)
        {
            return true;
        }

        var counts = new Dictionary<(string Id, string Path, string Message), int>();
        foreach (var key in GetReportedCompilerDiagnostics(compilation, ct))
        {
            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        foreach (var key in GetReportedCompilerDiagnostics(updatedCompilation, ct))
        {
            if (!counts.TryGetValue(key, out var count) || count == 0)
            {
                return true;
            }

            counts[key] = count - 1;
        }

        return false;
    }

    private static IEnumerable<(string Id, string Path, string Message)> GetReportedCompilerDiagnostics(Compilation compilation, CancellationToken ct) =>
        compilation.GetDiagnostics(ct)
            .Where(static diagnostic => !diagnostic.IsSuppressed && diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(static diagnostic => (
                diagnostic.Id,
                diagnostic.Location.SourceTree?.FilePath ?? string.Empty,
                diagnostic.GetMessage(CultureInfo.InvariantCulture)));
}
