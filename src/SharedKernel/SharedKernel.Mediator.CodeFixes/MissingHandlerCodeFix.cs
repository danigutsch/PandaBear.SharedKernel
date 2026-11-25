using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Generates a missing handler when a request has no existing handler candidates.
/// </summary>
internal static class MissingHandlerCodeFix
{
    private const string NotImplementedBody = "throw new global::System.NotImplementedException();";

    /// <summary>
    /// Registers the missing-handler code fix when the request shape supports a safe generated stub.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The missing-handler diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        var requestNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var requestDeclaration = requestNode.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
                                 ?? requestNode as Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax;

        if (requestDeclaration is null)
        {
            return;
        }

        if (semanticModel.GetDeclaredSymbol(requestDeclaration, context.CancellationToken) is not INamedTypeSymbol requestTypeSymbol)
        {
            return;
        }

        if (!TryCreatePlan(requestTypeSymbol, semanticModel.Compilation, out var plan))
        {
            return;
        }

        if (HasExistingHandlerCandidate(requestTypeSymbol, semanticModel.Compilation))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Generate handler '{plan.HandlerName}'",
                createChangedSolution: cancellationToken => AddHandlerDocumentAsync(document, plan, cancellationToken),
                equivalenceKey: $"GenerateHandler:{plan.HandlerName}"),
            diagnostic);
    }

    private static Task<Solution> AddHandlerDocumentAsync(Document document, GenerationPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = $"{plan.HandlerName}.cs";
        var filePath = document.FilePath is null
            ? fileName
            : Path.Combine(Path.GetDirectoryName(document.FilePath) ?? string.Empty, fileName);
        var sourceText = SourceText.From(plan.Render(), Encoding.UTF8);
        var newDocument = document.Project.AddDocument(fileName, sourceText, document.Folders, filePath);

        return Task.FromResult(newDocument.Project.Solution);
    }

    private static bool TryCreatePlan(INamedTypeSymbol requestTypeSymbol, Compilation compilation, out GenerationPlan plan)
    {
        var requestTypeName = requestTypeSymbol.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability);
        var namespaceName = requestTypeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : requestTypeSymbol.ContainingNamespace.ToDisplayString();
        var handlerName = requestTypeSymbol.Name + "Handler";

        foreach (var mediatorInterface in requestTypeSymbol.AllInterfaces)
        {
            if (SymbolMatches(compilation, mediatorInterface, MetadataNames.Query))
            {
                var responseTypeName = mediatorInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability);
                plan = new GenerationPlan(
                    handlerName,
                    namespaceName,
                    $"global::SharedKernel.Mediator.IQueryHandler<{requestTypeName}, {responseTypeName}>",
                    $"global::System.Threading.Tasks.ValueTask<{responseTypeName}>",
                    requestTypeName,
                    NotImplementedBody);
                return true;
            }

            if (SymbolMatches(compilation, mediatorInterface, MetadataNames.CommandOfResponse))
            {
                var responseTypeName = mediatorInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability);
                plan = new GenerationPlan(
                    handlerName,
                    namespaceName,
                    $"global::SharedKernel.Mediator.ICommandHandler<{requestTypeName}, {responseTypeName}>",
                    $"global::System.Threading.Tasks.ValueTask<{responseTypeName}>",
                    requestTypeName,
                    NotImplementedBody);
                return true;
            }

            if (SymbolMatches(compilation, mediatorInterface, MetadataNames.Command))
            {
                plan = new GenerationPlan(
                    handlerName,
                    namespaceName,
                    $"global::SharedKernel.Mediator.ICommandHandler<{requestTypeName}>",
                    "global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit>",
                    requestTypeName,
                    NotImplementedBody);
                return true;
            }

            if (SymbolMatches(compilation, mediatorInterface, MetadataNames.Request))
            {
                var responseTypeName = mediatorInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability);
                plan = new GenerationPlan(
                    handlerName,
                    namespaceName,
                    $"global::SharedKernel.Mediator.IRequestHandler<{requestTypeName}, {responseTypeName}>",
                    $"global::System.Threading.Tasks.ValueTask<{responseTypeName}>",
                    requestTypeName,
                    NotImplementedBody);
                return true;
            }
        }

        plan = default;
        return false;
    }

    private static bool HasExistingHandlerCandidate(INamedTypeSymbol requestTypeSymbol, Compilation compilation)
    {
        foreach (var candidateType in EnumerateTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var candidateInterface in candidateType.AllInterfaces)
            {
                if (!TryGetHandledRequestType(candidateInterface, compilation, out var handledRequestType))
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(handledRequestType, requestTypeSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol @namespace)
    {
        foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
        {
            foreach (var nestedType in EnumerateTypes(nestedNamespace))
            {
                yield return nestedType;
            }
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            foreach (var nestedType in EnumerateTypes(type))
            {
                yield return nestedType;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nestedType in type.GetTypeMembers())
        {
            foreach (var childType in EnumerateTypes(nestedType))
            {
                yield return childType;
            }
        }
    }

    private static bool TryGetHandledRequestType(
        INamedTypeSymbol candidateInterface,
        Compilation compilation,
        out ITypeSymbol? handledRequestType)
    {
        if (SymbolMatches(compilation, candidateInterface, MetadataNames.RequestHandler)
            || SymbolMatches(compilation, candidateInterface, MetadataNames.QueryHandler)
            || SymbolMatches(compilation, candidateInterface, MetadataNames.CommandHandlerOfResponse))
        {
            handledRequestType = candidateInterface.TypeArguments[0];
            return true;
        }

        if (SymbolMatches(compilation, candidateInterface, MetadataNames.CommandHandler))
        {
            handledRequestType = candidateInterface.TypeArguments[0];
            return true;
        }

        handledRequestType = null;
        return false;
    }

    private static bool SymbolMatches(Compilation compilation, INamedTypeSymbol candidate, string metadataName)
    {
        var expected = compilation.GetTypeByMetadataName(metadataName);
        return expected is not null && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, expected);
    }

    private readonly struct GenerationPlan(
        string handlerName,
        string? namespaceName,
        string handlerInterface,
        string returnType,
        string requestType,
        string methodBody)
    {
        public string HandlerName { get; } = handlerName;

        public string? NamespaceName { get; } = namespaceName;

        public string HandlerInterface { get; } = handlerInterface;

        public string ReturnType { get; } = returnType;

        public string RequestType { get; } = requestType;

        public string MethodBody { get; } = methodBody;

        public string Render()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(NamespaceName))
            {
                builder.Append("namespace ")
                    .Append(NamespaceName)
                    .AppendLine(";")
                    .AppendLine();
            }

            builder.Append("public sealed class ")
                .Append(HandlerName)
                .Append(" : ")
                .Append(HandlerInterface)
                .AppendLine()
                .AppendLine("{")
                .Append("    public ")
                .Append(ReturnType)
                .Append(" Handle(")
                .Append(RequestType)
                .AppendLine(" request, global::System.Threading.CancellationToken ct)")
                .AppendLine("    {")
                .Append("        ")
                .Append(MethodBody)
                .AppendLine()
                .AppendLine("    }")
                .AppendLine("}");

            return builder.ToString();
        }
    }
}
