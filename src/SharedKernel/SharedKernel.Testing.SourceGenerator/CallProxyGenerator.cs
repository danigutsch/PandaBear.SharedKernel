using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.Testing.SourceGenerator;

/// <summary>
/// Generates a typed <c>CallProxy</c> for every interface that a test wraps with <c>CallProxy.Create&lt;T&gt;</c> or
/// <c>WrapWithCallProxy&lt;T&gt;</c>, or hooks through <c>CallHooks&lt;T&gt;</c>: a class that implements the interface, forwards each
/// call and runs the hooks, the typed hooks members of <c>CallHooks&lt;T&gt;</c>, and a module initializer that registers them.
/// </summary>
[Generator]
public sealed class CallProxyGenerator : IIncrementalGenerator
{
    private const string ContainerPrefix = "CallProxyFor_";
    private const string HintNameFormat = "CallProxy.{0}.g.cs";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entrySites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => CallProxySiteReader.IsEntryCandidate(node),
                static (syntaxContext, cancellationToken) => CallProxySiteReader.ReadEntry(syntaxContext, cancellationToken))
            .Where(static site => site is not null)
            .WithTrackingName("CallProxyEntrySites");
        var hookSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => CallProxySiteReader.IsHookCandidate(node),
                static (syntaxContext, cancellationToken) => CallProxySiteReader.ReadHook(syntaxContext, cancellationToken))
            .Where(static site => site is not null)
            .WithTrackingName("CallProxyHookSites");
        var sites = entrySites.Collect()
            .Combine(hookSites.Collect())
            .WithTrackingName("CallProxySites");

        context.RegisterSourceOutput(sites, static (productionContext, input) => Produce(productionContext, input.Left.AddRange(input.Right)));
    }

    private static void Produce(SourceProductionContext context, ImmutableArray<ProxySite?> sites)
    {
        foreach (var diagnostic in sites
            .SelectMany(static site => site!.Diagnostics)
            .Distinct()
            .OrderBy(static diagnostic => diagnostic.Location?.FilePath, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location?.Span.Start)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(static diagnostic => string.Join("|", diagnostic.Arguments), StringComparer.Ordinal))
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        var models = sites
            .Select(static site => site!.Model)
            .OfType<ProxyModel>()
            .GroupBy(static model => model.Key, StringComparer.Ordinal)
            .Select(static group => group.OrderBy(static model => model.ServiceType, StringComparer.Ordinal).First())
            .OrderBy(static model => model.Key, StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            var name = ProxyNaming.Unique(ProxyNaming.Sanitize(model.Key.Replace("global::", string.Empty)), usedNames);
            context.AddSource(
                string.Format(CultureInfo.InvariantCulture, HintNameFormat, name),
                SourceText.From(ProxyEmitter.Emit(model, ContainerPrefix + name), Encoding.UTF8));
        }
    }
}
