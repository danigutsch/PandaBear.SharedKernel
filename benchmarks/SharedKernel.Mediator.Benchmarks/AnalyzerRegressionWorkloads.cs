using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Aspire.Analyzers;
using SharedKernel.AspNetCore.Analyzers;
using SharedKernel.Blazor.Analyzers;
using SharedKernel.Domain.Analyzers;
using SharedKernel.EntityFrameworkCore.Analyzers;
using SharedKernel.IO.Analyzers;
using SharedKernel.Mediator.Analyzers;
using SharedKernel.Results.Analyzers;
using SharedKernel.Style.Analyzers;
using SharedKernel.Testing.Analyzers;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>Provides representative diagnostic-producing synthetic projects for the shipped analyzers.</summary>
internal static class AnalyzerRegressionWorkloads
{
    private const string EfModelSymbols = """
        namespace SharedKernel.EntityFrameworkCore
        {
            public static class UniqueRuleIndexBuilderExtensions { }
        }

        namespace Microsoft.EntityFrameworkCore
        {
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class IndexAttribute(string property) : Attribute
            {
                public string Property { get; } = property;
                public bool IsUnique { get; set; }
            }
        }
        """;

    /// <summary>Creates one source workload per diagnostic analyzer at the requested scale.</summary>
    /// <param name="sourceCount">Number of repeated declarations or operations in a workload.</param>
    public static (DiagnosticAnalyzer Analyzer, string Source)[] Create(int sourceCount) =>
    [
        (new RuntimeEndpointBindingAnalyzer(),
            "using Microsoft.AspNetCore.Builder; using Microsoft.AspNetCore.Routing;\n"
            + Repeat(sourceCount, static id => $$"""
                public static class Routes{{id}}
                {
                    public static void Register(IEndpointRouteBuilder routes, Delegate handler)
                        => routes.MapGet("/sample", handler);
                }
                """)),
        (new AspireAnalyzer(),
            "public class Resource { public Resource WithImageTag(string tag) => this; } "
            + "public class Host { public void Build(Resource resource) {"
            + string.Concat(Enumerable.Repeat("resource.WithImageTag(\"latest\");", sourceCount)) + "} }"),
        (new BlazorAnalyzer(),
            "using Microsoft.AspNetCore.Components;\n" + Repeat(sourceCount, static id => $$"""
                public sealed class Page{{id}} : ComponentBase
                {
                    [Inject]
                    public SharedKernel.Mediator.ISender Sender { get; set; } = default!;
                }
                """)),
        (new DomainAnalyzer(),
            Repeat(sourceCount, static id => $$"""
                public sealed class Tour{{id}}
                {
                    public DateTimeOffset Stamp() => DateTimeOffset.UtcNow;
                }
                """)),
        (new EntityFrameworkCoreAnalyzer(),
            EfModelSymbols + "\n" + Repeat(sourceCount, static id => $$"""
                [Microsoft.EntityFrameworkCore.Index(nameof(Customer{{id}}.Email), IsUnique = true)]
                public sealed class Customer{{id}}
                {
                    public string Email { get; set; } = string.Empty;
                }
                """)),
        (new IOAnalyzer(),
            "using System.Net.Http;\n" + Repeat(sourceCount, static id => $$"""
                public sealed class Intake{{id}}
                {
                    public Task<byte[]> Load(HttpContent content) => content.ReadAsByteArrayAsync();
                }
                """)),
        (new MediatorAnalyzer(), AnalyzerBenchmarkSourceFactory.CreateSource(sourceCount, AnalyzerBenchmarkSourceFactory.ManyDiagnostics)),
        (new ResultsAnalyzer(),
            "using SharedKernel.Results;\n" + Repeat(sourceCount, static id => $$"""
                public sealed class Handler{{id}}
                {
                    public void Execute() { Run(); }
                    private static Result Run() => Result.Ok();
                }
                """)),
        (new StyleAnalyzer(),
            Repeat(sourceCount, static id => $"public class Clock{id} {{ public object ReadAsync() => DateTimeOffset.UtcNow; }}")),
        (new TestingAnalyzer(),
            "namespace Xunit { public class FactAttribute : Attribute {} } namespace Cases {"
            + Repeat(sourceCount, static id => $"public class Case{id} {{ [Xunit.Fact] public void BadName() {{ var sut = new object(); }} }}")
            + "}"),
    ];

    private static string Repeat(int sourceCount, Func<string, string> declaration) =>
        string.Join(Environment.NewLine, Enumerable.Range(0, sourceCount)
            .Select(i => declaration(i.ToString(CultureInfo.InvariantCulture))));
}
