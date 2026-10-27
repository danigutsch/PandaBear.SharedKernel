using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.AuditTrail.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.AuditTrail.GeneratorTests;

/// <summary>
/// Compiles audit trail sources and runs AuditTrailMappingGenerator over them for the generator tests.
/// </summary>
internal static class AuditTrailGeneratorTestHarness
{
    /// <summary>
    /// Hint name of the source file that AuditTrailMappingGenerator emits.
    /// </summary>
    public const string GeneratedHintName = "SharedKernel.AuditTrail.GeneratedAuditTrailMappings.g.cs";

    private const string DefaultUsings = """
        using System;
        using PandaBear.AuditTrail;
        using PandaBear.Domain;

        """;

    /// <summary>
    /// Parses the source with default usings under the preview language version and compiles it with references to the audit trail, domain and dependency injection assemblies.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation(
            [syntaxTree],
            "SharedKernel.AuditTrail.Tests.Dynamic",
            [
                MetadataReference.CreateFromFile(typeof(IAuditTrailEntry).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Domain.IDomainEvent).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            ]);
    }

    /// <summary>
    /// Runs AuditTrailMappingGenerator over the compilation and returns the run result, updated compilation and diagnostics.
    /// </summary>
    public static AuditTrailGeneratorRun RunGeneratorDriver(CSharpCompilation compilation)
    {
        var runResult = SharedHarness.RunAndUpdate([new AuditTrailMappingGenerator()], compilation, out var outputCompilation, out var diagnostics);

        return new AuditTrailGeneratorRun(runResult, outputCompilation, diagnostics);
    }

    /// <summary>
    /// Returns the text of the generated audit trail mappings file from the generator run result.
    /// </summary>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);
}

/// <summary>
/// Captures the outcome of one AuditTrailMappingGenerator run.
/// </summary>
/// <param name="RunResult">The raw generator driver result.</param>
/// <param name="OutputCompilation">The compilation updated with the generated sources.</param>
/// <param name="Diagnostics">The diagnostics reported by the generator run.</param>
internal sealed record AuditTrailGeneratorRun(
    GeneratorDriverRunResult RunResult,
    CSharpCompilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics);
