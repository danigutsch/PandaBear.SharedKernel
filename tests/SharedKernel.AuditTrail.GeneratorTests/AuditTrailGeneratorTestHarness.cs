using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.AuditTrail.SourceGenerator;
using SharedKernel.DomainEvents;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.AuditTrail.GeneratorTests;

internal static class AuditTrailGeneratorTestHarness
{
    public const string GeneratedHintName = "SharedKernel.AuditTrail.GeneratedAuditTrailMappings.g.cs";

    private const string DefaultUsings = """
        using System;
        using SharedKernel.AuditTrail;
        using SharedKernel.Domain;

        """;

    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return CSharpCompilation.Create(
            assemblyName: "SharedKernel.AuditTrail.Tests.Dynamic",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static AuditTrailGeneratorRun RunGeneratorDriver(CSharpCompilation compilation)
    {
        var generator = new AuditTrailMappingGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: [],
            parseOptions: (CSharpParseOptions?)compilation.SyntaxTrees.First().Options,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>.Empty));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return new AuditTrailGeneratorRun(
            driver.GetRunResult(),
            (CSharpCompilation)outputCompilation,
            diagnostics);
    }

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult)
    {
        var generatedSource = runResult.Results.Single().GeneratedSources.SingleOrDefault(
            source => string.Equals(source.HintName, GeneratedHintName, StringComparison.Ordinal));

        return generatedSource.SourceText is null
            ? throw new InvalidOperationException($"Generated source not found: {GeneratedHintName}")
            : generatedSource.SourceText.ToString();
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        ArgumentException.ThrowIfNullOrEmpty(trustedPlatformAssemblies);

        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(IAuditTrailEntry).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Domain.IDomainEvent).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IDomainEventDispatchHandler).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location));
        return references;
    }
}

internal sealed record AuditTrailGeneratorRun(
    GeneratorDriverRunResult RunResult,
    CSharpCompilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics);
