using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.AuditTrail.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.AuditTrail.GeneratorTests;

internal static class AuditTrailGeneratorTestHarness
{
    public const string GeneratedHintName = "SharedKernel.AuditTrail.GeneratedAuditTrailMappings.g.cs";

    private const string DefaultUsings = """
        using System;
        using PandaBear.AuditTrail;
        using PandaBear.Domain;

        """;

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

    public static AuditTrailGeneratorRun RunGeneratorDriver(CSharpCompilation compilation)
    {
        var runResult = SharedHarness.RunAndUpdate([new AuditTrailMappingGenerator()], compilation, out var outputCompilation, out var diagnostics);

        return new AuditTrailGeneratorRun(runResult, outputCompilation, diagnostics);
    }

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);
}

internal sealed record AuditTrailGeneratorRun(
    GeneratorDriverRunResult RunResult,
    CSharpCompilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics);
