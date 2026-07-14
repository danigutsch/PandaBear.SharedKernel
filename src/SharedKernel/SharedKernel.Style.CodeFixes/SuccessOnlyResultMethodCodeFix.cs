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
    private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";
    private const string ValueTaskOfTMetadataName = "System.Threading.Tasks.ValueTask`1";

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
                title: methodSymbol.IsAsync
                    ? $"Convert command method to {methodSymbol.ReturnType.Name}"
                    : "Convert command method to void",
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
            || targetMethod.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.VirtualKeyword)
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
            || targetSymbol.IsGenericMethod
            || targetSymbol.PartialDefinitionPart is not null
            || targetSymbol.PartialImplementationPart is not null)
        {
            return false;
        }

        var resultType = semanticModel.Compilation.GetTypeByMetadataName(ResultMetadataName);
        if (resultType is null
            || !IsSupportedTargetReturnType(targetSymbol, resultType, semanticModel.Compilation)
            || !IsResultOkInvocation(returnInvocation, semanticModel, resultType, cancellationToken))
        {
            return false;
        }

        methodSymbol = targetSymbol;
        return true;
    }

    private static bool IsSupportedTargetReturnType(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol resultType,
        Compilation compilation)
    {
        if (!methodSymbol.IsAsync)
        {
            return SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, resultType);
        }

        return IsTaskOrValueTaskOfResult(methodSymbol.ReturnType, resultType, compilation);
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
            if (!await TryAppendReferenceEdits(
                edits,
                referencedSymbol,
                solution,
                methodSymbol,
                resultType,
                cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        return edits;
    }

    private static async Task<bool> TryAppendReferenceEdits(
        List<ReferenceEdit> edits,
        ReferencedSymbol referencedSymbol,
        Solution solution,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol resultType,
        CancellationToken cancellationToken)
    {
        foreach (var reference in referencedSymbol.Locations)
        {
            if (reference.IsImplicit || !reference.Location.IsInSource)
            {
                return false;
            }

            var document = solution.GetDocument(reference.Document.Id);
            if (document is null)
            {
                return false;
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
                    methodSymbol.IsAsync,
                    cancellationToken,
                    out var edit))
            {
                return false;
            }

            if (edit is not null)
            {
                edits.Add(edit);
            }
        }

        return true;
    }

    private static bool TryCreateReferenceEdit(
        SyntaxNode root,
        SemanticModel semanticModel,
        DocumentId documentId,
        Location location,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol resultType,
        bool isAsyncTarget,
        CancellationToken cancellationToken,
        out ReferenceEdit? edit)
    {
        edit = null;

        var callExpression = TryGetCallExpression(root, semanticModel, location, methodSymbol, isAsyncTarget, cancellationToken);
        if (callExpression is null)
        {
            return false;
        }

        if (callExpression.Parent is ExpressionStatementSyntax)
        {
            return true;
        }

        if (TryCreateFailureGuardEdit(root, semanticModel, documentId, callExpression, resultType, isAsyncTarget, cancellationToken, out edit))
        {
            return true;
        }

        if (callExpression.Parent is ReturnStatementSyntax)
        {
            edit = TryCreateReturnReferenceEdit(
                semanticModel,
                documentId,
                callExpression,
                resultType,
                isAsyncTarget,
                cancellationToken);
            return edit is not null;
        }

        return TryCreateLambdaExpressionReferenceEdit(
            semanticModel,
            documentId,
            callExpression,
            resultType,
            isAsyncTarget,
            out edit);
    }

    private static ExpressionSyntax? TryGetCallExpression(
        SyntaxNode root,
        SemanticModel semanticModel,
        Location location,
        IMethodSymbol methodSymbol,
        bool isAsyncTarget,
        CancellationToken cancellationToken)
    {
        var invocation = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null
            || !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol, methodSymbol))
        {
            return null;
        }

        if (invocation.Parent is not AwaitExpressionSyntax awaitExpression)
        {
            return invocation;
        }

        return isAsyncTarget ? awaitExpression : null;
    }

    private static ReferenceEdit? TryCreateReturnReferenceEdit(
        SemanticModel semanticModel,
        DocumentId documentId,
        ExpressionSyntax callExpression,
        INamedTypeSymbol resultType,
        bool isAsyncTarget,
        CancellationToken cancellationToken)
    {
        if (callExpression.Parent is not ReturnStatementSyntax { Expression: { } returnExpression } returnStatement
            || returnExpression != callExpression
            || (isAsyncTarget && callExpression is not AwaitExpressionSyntax)
            || returnStatement.Parent is not BlockSyntax)
        {
            return null;
        }

        var enclosingSymbol = semanticModel.GetEnclosingSymbol(returnStatement.SpanStart, cancellationToken);
        if (enclosingSymbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction }
            && returnStatement.FirstAncestorOrSelf<LambdaExpressionSyntax>() is { } lambda)
        {
            return ReturnsResultOrTaskOfResult(lambda, semanticModel, resultType)
                ? new ReferenceEdit(documentId, returnStatement, ReferenceEditKind.LambdaReturn, callExpression)
                : null;
        }

        if (enclosingSymbol is not IMethodSymbol
            {
                MethodKind: MethodKind.Ordinary,
                ReturnType: { } returnType,
            }
            || !ReturnsResultOrAsyncResult(returnType, semanticModel.Compilation, resultType))
        {
            return null;
        }

        return new ReferenceEdit(documentId, returnStatement, ReferenceEditKind.MethodReturn, callExpression);
    }

    private static bool TryCreateLambdaExpressionReferenceEdit(
        SemanticModel semanticModel,
        DocumentId documentId,
        ExpressionSyntax callExpression,
        INamedTypeSymbol resultType,
        bool isAsyncTarget,
        out ReferenceEdit? edit)
    {
        edit = null;
        if (callExpression.Parent is not LambdaExpressionSyntax lambdaExpression
            || lambdaExpression.Body != callExpression
            || (isAsyncTarget && callExpression is not AwaitExpressionSyntax)
            || !ReturnsResultOrTaskOfResult(lambdaExpression, semanticModel, resultType))
        {
            return false;
        }

        edit = new ReferenceEdit(documentId, lambdaExpression, ReferenceEditKind.LambdaExpression, callExpression);
        return true;
    }

    private static bool TryCreateFailureGuardEdit(
        SyntaxNode root,
        SemanticModel semanticModel,
        DocumentId documentId,
        ExpressionSyntax callExpression,
        INamedTypeSymbol resultType,
        bool isAsyncTarget,
        CancellationToken cancellationToken,
        out ReferenceEdit? edit)
    {
        edit = null;

        if ((isAsyncTarget && callExpression is not AwaitExpressionSyntax)
            || callExpression.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variableDeclarator }
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

        edit = new ReferenceEdit(documentId, declaration, ReferenceEditKind.FailureGuard, callExpression, failureGuard);
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
            || !ReturnsResultOrAsyncResult(returnType, semanticModel.Compilation, resultType))
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

                ReplaceTargetMethod(editor, methodDeclaration, methodSymbol);
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

    private static void ReplaceTargetMethod(
        DocumentEditor editor,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol)
    {
        var returnStatement = (ReturnStatementSyntax)methodDeclaration.Body!.Statements[methodDeclaration.Body.Statements.Count - 1];
        var replacementReturnType = CreateReplacementReturnType(methodSymbol)
            .WithTriviaFrom(methodDeclaration.ReturnType)
            .WithAdditionalAnnotations(Formatter.Annotation);
        editor.ReplaceNode(methodDeclaration.ReturnType, replacementReturnType);
        editor.RemoveNode(returnStatement, SyntaxRemoveOptions.KeepExteriorTrivia);
    }

    private static TypeSyntax CreateReplacementReturnType(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsAsync)
        {
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        }

        return methodSymbol.ReturnType.Name switch
        {
            "Task" => SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task"),
            "ValueTask" => SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.ValueTask"),
            _ => throw new InvalidOperationException("Unsupported async command return type."),
        };
    }

    private static void ApplyReferenceEdit(DocumentEditor editor, ReferenceEdit edit)
    {
        switch (edit.Kind)
        {
            case ReferenceEditKind.MethodReturn:
            case ReferenceEditKind.LambdaReturn:
                var returnStatement = (ReturnStatementSyntax)edit.Node;
                editor.InsertBefore(returnStatement, CreateInvocationStatement(edit.CallExpression));
                editor.ReplaceNode(returnStatement, returnStatement.WithExpression(CreateSuccessResult()).WithAdditionalAnnotations(Formatter.Annotation));
                break;
            case ReferenceEditKind.LambdaExpression:
                var lambdaExpression = (LambdaExpressionSyntax)edit.Node;
                var block = SyntaxFactory.Block(
                    CreateInvocationStatement(edit.CallExpression),
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
                var invocationStatement = CreateInvocationStatement(edit.CallExpression)
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

        return ReturnsResultOrAsyncResult(returnType, semanticModel.Compilation, resultType);
    }

    private static bool ReturnsResultOrAsyncResult(
        ITypeSymbol returnType,
        Compilation compilation,
        INamedTypeSymbol resultType)
    {
        return SymbolEqualityComparer.Default.Equals(returnType, resultType)
            || IsTaskOrValueTaskOfResult(returnType, resultType, compilation);
    }

    private static bool IsTaskOrValueTaskOfResult(
        ITypeSymbol returnType,
        INamedTypeSymbol resultType,
        Compilation compilation)
    {
        var taskOfT = compilation.GetTypeByMetadataName(TaskOfTMetadataName);
        var valueTaskOfT = compilation.GetTypeByMetadataName(ValueTaskOfTMetadataName);
        return returnType is INamedTypeSymbol genericReturnType
            && genericReturnType.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(genericReturnType.TypeArguments[0], resultType)
            && (SymbolEqualityComparer.Default.Equals(genericReturnType.OriginalDefinition, taskOfT)
                || SymbolEqualityComparer.Default.Equals(genericReturnType.OriginalDefinition, valueTaskOfT));
    }

    private static ExpressionStatementSyntax CreateInvocationStatement(ExpressionSyntax callExpression)
    {
        return SyntaxFactory.ExpressionStatement(callExpression)
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
        ExpressionSyntax callExpression,
        IfStatementSyntax? guard = null)
    {
        public DocumentId DocumentId { get; } = documentId;

        public SyntaxNode Node { get; } = node;

        public ReferenceEditKind Kind { get; } = kind;

        public ExpressionSyntax CallExpression { get; } = callExpression;

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
