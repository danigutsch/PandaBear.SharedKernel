using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Builds analyzer fixtures using the real dependency injection reference types.</summary>
internal static class DirectDependencyInjectionTestSource
{
    /// <summary>Diagnostic ID for the direct dependency injection rule.</summary>
    internal const string DiagnosticId = "SKTEST023";

    /// <summary>Compiles test members with the requested diagnostic severity and source path.</summary>
    internal static CSharpCompilation Compile(string members, ReportDiagnostic? severity = ReportDiagnostic.Warn, string path = "TestSource.cs")
    {
        var compilation = AnalyzerTestHarness.CreateCompilation($$"""
            using Microsoft.Extensions.DependencyInjection;
            using Collection = Microsoft.Extensions.DependencyInjection.ServiceCollection;
            using TestCase = Xunit.FactAttribute;

            public sealed class Tests
            {
                private readonly IServiceCollection services = null!;
                private readonly IServiceProvider provider = null!;
                private readonly ServiceProvider concreteProvider = null!;
                private readonly IServiceScopeFactory scopeFactory = null!;
                private readonly IKeyedServiceProvider keyedProvider = null!;

                {{members}}
            }
            """, path: path);
        return severity is { } level
            ? compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, level)))
            : compilation;
    }
}
