using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the signature-changing code fix for private command methods that always return <c>Result.Ok()</c>.
/// </summary>
internal static class SuccessOnlyResultMethodCodeFix
{
    private const string ResultMetadataName = "SharedKernel.Results.Result";

    /// <summary>
    /// Registers a code action when all source references can preserve their successful result behavior.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null
            || semanticModel is null
            || !TryGetTargetMethod(root, semanticModel, diagnostic, context.CancellationToken, out var methodSymbol))
        {
            return;
        }

        var resultType = semanticModel.Compilation.GetTypeByMetadataName(ResultMetadataName);
        var referenceEdits = await TryGetReferenceEdits(
            context.Document.Project.Solution,
            methodSymbol,
            resultType,
            context.CancellationToken).ConfigureAwait(false);
        if (referenceEdits is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert command method to void",
                createChangedSolution: cancellationToken => Apply(
                    context.Document,
                    methodSymbol,
                    referenceEdits,
                    cancellationToken),
                equivalenceKey: "ConvertSuccessOnlyResultMethodToVoid"),
            diagnostic);
    }

    private static bool TryGetTargetMethod(
        SyntaxNode root,
        SemanticModel semanticModel,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out IMethodSymbol methodSymbol)
    {
        methodSymbol = null!;

        var targetMethod = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (targetMethod is null
            || targetMethod.Body is null
            || targetMethod.ExpressionBody is not null
            || targetMethod.TypeParameterList is not null
            || targetMethod.Modifiers.All(static modifier => !modifier.IsKind(SyntaxKind.PrivateKeyword))
            || targetMethod.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.AsyncKeyword)
                || modifier.IsKind(SyntaxKind.VirtualKeyword)
                || modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword))
            || targetMethod.Body.Statements.LastOrDefault() is not ReturnStatementSyntax returnStatement
            || returnStatement.Expression is not InvocationExpressionSyntax returnInvocation
            || targetMethod.Body.DescendantNodes().OfType<ReturnStatementSyntax>().Count() != 1
            || semanticModel.GetDeclaredSymbol(targetMethod, cancellationToken) is not IMethodSymbol targetSymbol
            || targetSymbol.MethodKind != MethodKind.Ordinary
            || targetSymbol.DeclaredAccessibility != Accessibility.Private
            || targetSymbol.IsOverride
            || targetSymbol.IsAbstract
            || targetSymbol.IsVirtual
            || targetSymbol.IsAsync
            || targetSymbol.IsGenericMethod
            || targetSymbol.PartialDefinitionPart is not null
            || targetSymbol.PartialImplementationPart is not null)
        {
            return false;
        }

        var resultType = semanticModel.Compilation.GetTypeByMetadataName(ResultMetadataName);
        if (resultType is null
            || !SymbolEqualityComparer.Default.Equals(targetSymbol.ReturnType, resultType)
            || !IsResultOkInvocation(returnInvocation, semanticModel, resultType, cancellationToken))
        {
            return false;
        }

        methodSymbol = targetSymbol;
        return true;
    }

    private static async Task<IReadOnlyList<ReferenceEdit>?> TryGetReferenceEdits(
        Solution solution,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol? resultType,
        CancellationToken cancellationToken)
    {
        if (resultType is null)
        {
            return null;
        }

        var edits = new List<ReferenceEdit>();
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(methodSymbol, solution, cancellationToken).ConfigureAwait(false);
        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var reference in referencedSymbol.Locations)
            {
                if (reference.IsImplicit || !reference.Location.IsInSource)
                {
                    return null;
                }

                var document = solution.GetDocument(reference.Document.Id);
                if (document is null)
                {
                    return null;
                }

                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (root is null
                    || semanticModel is null
                    || !TryCreateReferenceEdit(
                        root,
                        semanticModel,
                        document.Id,
                        reference.Location,
                        methodSymbol,
                        resultType,
                        cancellationToken,
                        out var edit))
                {
                    return null;
                }

                if (edit is not null)
                {
                    edits.Add(edit);
                }
            }
        }

        return edits;
    }

    private static bool TryCreateReferenceEdit(
        SyntaxNode root,
        SemanticModel semanticModel,
        DocumentId documentId,
        Location location,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol resultType,
        CancellationToken cancellationToken,
        out ReferenceEdit? edit)
    {
        edit = null;

        var invocation = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null
            || !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol, methodSymbol))
        {
            return false;
        }

        if (invocation.Parent is ExpressionStatementSyntax)
        {
            return true;
        }

        if (TryCreateFailureGuardEdit(root, semanticModel, documentId, invocation, resultType, cancellationToken, out edit))
        {
            return true;
        }

        if (invocation.Parent is ReturnStatementSyntax { Expression: { } returnExpression } returnStatement
            && returnExpression == invocation)
        {
            if (returnStatement.Parent is not BlockSyntax)
            {
                return false;
            }

            if (semanticModel.GetEnclosingSymbol(returnStatement.SpanStart, cancellationToken) is IMethodSymbol
                {
                    MethodKind: MethodKind.AnonymousFunction,
                }
                && returnStatement.FirstAncestorOrSelf<LambdaExpressionSyntax>() is { } lambda)
            {
                if (!ReturnsResultOrTaskOfResult(lambda, semanticModel, resultType))
                {
                    return false;
                }

                edit = new ReferenceEdit(documentId, returnStatement, ReferenceEditKind.LambdaReturn, invocation);
                return true;
            }

            if (semanticModel.GetEnclosingSymbol(returnStatement.SpanStart, cancellationToken) is not IMethodSymbol
                {
                    MethodKind: MethodKind.Ordinary,
                    ReturnType: { } returnType,
                }
                || !SymbolEqualityComparer.Default.Equals(returnType, resultType))
            {
                return false;
            }

            edit = new ReferenceEdit(documentId, returnStatement, ReferenceEditKind.MethodReturn, invocation);
            return true;
        }

        if (invocation.Parent is LambdaExpressionSyntax lambdaExpression
            && lambdaExpression.Body == invocation
            && ReturnsResultOrTaskOfResult(lambdaExpression, semanticModel, resultType))
        {
            edit = new ReferenceEdit(documentId, lambdaExpression, ReferenceEditKind.LambdaExpression, invocation);
            return true;
        }

        return false;
    }

    private static bool TryCreateFailureGuardEdit(
        SyntaxNode root,
        SemanticModel semanticModel,
        DocumentId documentId,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol resultType,
        CancellationToken cancellationToken,
        out ReferenceEdit? edit)
    {
        edit = null;

        if (invocation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variableDeclarator }
            || variableDeclarator.Parent is not VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax declaration }
            || declaration.Declaration.Variables.Count != 1
            || declaration.Parent is not BlockSyntax block
            || semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken) is not ILocalSymbol local
            || !TryGetFollowingStatement(block, declaration, out var followingStatement)
            || followingStatement is not IfStatementSyntax { Else: null } failureGuard
            || !IsFailureGuard(failureGuard, local, semanticModel, resultType, cancellationToken)
            || CountLocalReferences(root, semanticModel, local, cancellationToken) != 2)
        {
            return false;
        }

        edit = new ReferenceEdit(documentId, declaration, ReferenceEditKind.FailureGuard, invocation, failureGuard);
        return true;
    }

    private static bool TryGetFollowingStatement(BlockSyntax block, StatementSyntax statement, out StatementSyntax followingStatement)
    {
        var index = block.Statements.IndexOf(statement);
        if (index < 0 || index == block.Statements.Count - 1)
        {
            followingStatement = null!;
            return false;
        }

        followingStatement = block.Statements[index + 1];
        return true;
    }

    private static bool IsFailureGuard(
        IfStatementSyntax failureGuard,
        ILocalSymbol local,
        SemanticModel semanticModel,
        INamedTypeSymbol resultType,
        CancellationToken cancellationToken)
    {
        if (failureGuard.Condition is not MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax conditionIdentifier,
                Name.Identifier.ValueText: "IsFailure",
            }
            || !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(conditionIdentifier, cancellationToken).Symbol, local)
            || !SymbolEqualityComparer.Default.Equals(local.Type, resultType)
            || GetGuardReturnStatement(failureGuard) is not { Expression: IdentifierNameSyntax returnIdentifier } returnStatement
            || !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(returnIdentifier, cancellationToken).Symbol, local)
            || semanticModel.GetEnclosingSymbol(returnStatement.SpanStart, cancellationToken) is not IMethodSymbol
            {
                MethodKind: MethodKind.Ordinary,
                ReturnType: { } returnType,
            }
            || !SymbolEqualityComparer.Default.Equals(returnType, resultType))
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(failureGuard.Condition, cancellationToken).Symbol is not IPropertySymbol failureProperty
            || !SymbolEqualityComparer.Default.Equals(failureProperty.ContainingType, resultType)
            || failureProperty.Name != "IsFailure")
        {
            return false;
        }

        return true;
    }

    private static ReturnStatementSyntax? GetGuardReturnStatement(IfStatementSyntax failureGuard)
    {
        return failureGuard.Statement switch
        {
            ReturnStatementSyntax returnStatement => returnStatement,
            BlockSyntax { Statements.Count: 1 } block when block.Statements[0] is ReturnStatementSyntax returnStatement => returnStatement,
            _ => null,
        };
    }

    private static int CountLocalReferences(
        SyntaxNode root,
        SemanticModel semanticModel,
        ILocalSymbol local,
        CancellationToken cancellationToken)
    {
        return root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Count(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                local));
    }

    private static async Task<Solution> Apply(
        Document document,
        IMethodSymbol methodSymbol,
        IReadOnlyList<ReferenceEdit> referenceEdits,
        CancellationToken cancellationToken)
    {
        var currentSolution = document.Project.Solution;
        var editsByDocument = referenceEdits.GroupBy(static edit => edit.DocumentId)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var documentIds = editsByDocument.Keys
            .Where(documentId => documentId != document.Id)
            .Prepend(document.Id);
        foreach (var documentId in documentIds)
        {
            var currentDocument = currentSolution.GetDocument(documentId);
            if (currentDocument is null)
            {
                return document.Project.Solution;
            }

            var editor = await DocumentEditor.CreateAsync(currentDocument, cancellationToken).ConfigureAwait(false);
            if (documentId == document.Id)
            {
                var currentRoot = await currentDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var currentSemanticModel = await currentDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (currentRoot is null
                    || currentSemanticModel is null
                    || !TryFindMethodDeclaration(currentRoot, currentSemanticModel, methodSymbol, cancellationToken, out var methodDeclaration))
                {
                    return document.Project.Solution;
                }

                ReplaceTargetMethod(editor, methodDeclaration);
            }

            if (editsByDocument.TryGetValue(documentId, out var documentEdits))
            {
                foreach (var edit in documentEdits)
                {
                    ApplyReferenceEdit(editor, edit);
                }
            }

            currentSolution = editor.GetChangedDocument().Project.Solution;
        }

        return currentSolution;
    }

    private static bool TryFindMethodDeclaration(
        SyntaxNode root,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out MethodDeclarationSyntax methodDeclaration)
    {
        var targetMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetDeclaredSymbol(candidate, cancellationToken),
                methodSymbol));
        if (targetMethod is null)
        {
            methodDeclaration = null!;
            return false;
        }

        methodDeclaration = targetMethod;
        return true;
    }

    private static void ReplaceTargetMethod(DocumentEditor editor, MethodDeclarationSyntax methodDeclaration)
    {
        var returnStatement = (ReturnStatementSyntax)methodDeclaration.Body!.Statements[methodDeclaration.Body.Statements.Count - 1];
        var voidReturnType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword))
            .WithTriviaFrom(methodDeclaration.ReturnType)
            .WithAdditionalAnnotations(Formatter.Annotation);
        editor.ReplaceNode(methodDeclaration.ReturnType, voidReturnType);
        editor.RemoveNode(returnStatement, SyntaxRemoveOptions.KeepExteriorTrivia);
    }

    private static void ApplyReferenceEdit(DocumentEditor editor, ReferenceEdit edit)
    {
        switch (edit.Kind)
        {
            case ReferenceEditKind.MethodReturn:
            case ReferenceEditKind.LambdaReturn:
                var returnStatement = (ReturnStatementSyntax)edit.Node;
                editor.InsertBefore(returnStatement, CreateInvocationStatement(edit.Invocation));
                editor.ReplaceNode(returnStatement, returnStatement.WithExpression(CreateSuccessResult()).WithAdditionalAnnotations(Formatter.Annotation));
                break;
            case ReferenceEditKind.LambdaExpression:
                var lambdaExpression = (LambdaExpressionSyntax)edit.Node;
                var block = SyntaxFactory.Block(
                    CreateInvocationStatement(edit.Invocation),
                    SyntaxFactory.ReturnStatement(CreateSuccessResult()))
                    .WithAdditionalAnnotations(Formatter.Annotation);
                editor.ReplaceNode(lambdaExpression, lambdaExpression.WithBody(block));
                break;
            case ReferenceEditKind.FailureGuard:
                if (edit.Guard is not IfStatementSyntax failureGuard)
                {
                    throw new InvalidOperationException("Failure guard syntax is required.");
                }

                var declaration = (LocalDeclarationStatementSyntax)edit.Node;
                var invocationStatement = CreateInvocationStatement(edit.Invocation)
                    .WithLeadingTrivia(declaration.GetLeadingTrivia())
                    .WithTrailingTrivia(declaration.GetTrailingTrivia());
                editor.ReplaceNode(declaration, invocationStatement);
                editor.RemoveNode(failureGuard, SyntaxRemoveOptions.KeepExteriorTrivia);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit));
        }
    }

    private static bool IsResultOkInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol resultType,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: "Ok",
            Parameters.Length: 0,
        } method
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, resultType)
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType);
    }

    private static bool ReturnsResultOrTaskOfResult(
        LambdaExpressionSyntax lambdaExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol resultType)
    {
        if (semanticModel.GetTypeInfo(lambdaExpression).ConvertedType is not INamedTypeSymbol
            {
                DelegateInvokeMethod.ReturnType: { } returnType,
            })
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, resultType))
        {
            return true;
        }

        var taskOfT = semanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskOfT = semanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        return returnType is INamedTypeSymbol genericReturnType
            && genericReturnType.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(genericReturnType.TypeArguments[0], resultType)
            && (SymbolEqualityComparer.Default.Equals(genericReturnType.OriginalDefinition, taskOfT)
                || SymbolEqualityComparer.Default.Equals(genericReturnType.OriginalDefinition, valueTaskOfT));
    }

    private static ExpressionStatementSyntax CreateInvocationStatement(InvocationExpressionSyntax invocation)
    {
        return SyntaxFactory.ExpressionStatement(invocation.WithoutTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static ExpressionSyntax CreateSuccessResult()
    {
        return SyntaxFactory.ParseExpression("global::SharedKernel.Results.Result.Ok()");
    }

    private sealed class ReferenceEdit(
        DocumentId documentId,
        SyntaxNode node,
        ReferenceEditKind kind,
        InvocationExpressionSyntax invocation,
        IfStatementSyntax? guard = null)
    {
        public DocumentId DocumentId { get; } = documentId;

        public SyntaxNode Node { get; } = node;

        public ReferenceEditKind Kind { get; } = kind;

        public InvocationExpressionSyntax Invocation { get; } = invocation;

        public IfStatementSyntax? Guard { get; } = guard;
    }

    private enum ReferenceEditKind
    {
        MethodReturn,
        LambdaReturn,
        LambdaExpression,
        FailureGuard,
    }
}
