using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.EntityFrameworkCore.Analyzers;

/// <summary>
/// Reports unique indexes in Entity Framework Core model configuration that are not tagged with an application rule through
/// <c>HasUniqueRule</c>.
/// </summary>
/// <remarks>The analyzer is inert unless the compilation references <c>PandaBear.EntityFrameworkCore.UniqueRuleIndexBuilderExtensions</c>.</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelEntityFrameworkCoreAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor UntaggedUniqueIndexRule = new(
        EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex,
        title: "Tag a unique index with a rule",
        messageFormat: "Unique index{0} is not tagged with a rule; add HasUniqueRule(...) so a lost uniqueness race is reported as UniqueConstraintViolationException instead of a raw provider exception{1}",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A unique index without a rule still enforces uniqueness in the database, but a violation cannot be reported by rule: it escapes as a raw provider exception and the client gets a server error. Call HasUniqueRule on the index builder so SaveChangesTranslatingUniqueViolations can report the rule.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } = ImmutableArray.Create(UntaggedUniqueIndexRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var compilation = compilationContext.Compilation;
            var ruleExtensions = compilation.GetTypeByMetadataName("PandaBear.EntityFrameworkCore.UniqueRuleIndexBuilderExtensions");
            if (ruleExtensions is null)
            {
                return;
            }

            var indexBuilder = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Metadata.Builders.IndexBuilder");
            var indexBuilderOfT = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Metadata.Builders.IndexBuilder`1");
            if (indexBuilder is not null || indexBuilderOfT is not null)
            {
                compilationContext.RegisterSyntaxNodeAction(
                    nodeContext => UniqueIndexChain.Analyze(nodeContext, UntaggedUniqueIndexRule, ruleExtensions, indexBuilder, indexBuilderOfT),
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
            }

            var indexAttribute = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IndexAttribute");
            if (indexAttribute is not null)
            {
                compilationContext.RegisterSyntaxNodeAction(
                    nodeContext => UniqueIndexChain.AnalyzeAttribute(nodeContext, UntaggedUniqueIndexRule, indexAttribute),
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.Attribute);
            }
        });
    }
}
