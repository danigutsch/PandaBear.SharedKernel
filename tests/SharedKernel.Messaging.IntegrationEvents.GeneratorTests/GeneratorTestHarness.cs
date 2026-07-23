using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Mediator;
using SharedKernel.Messaging.IntegrationEvents.SourceGenerator;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Messaging.IntegrationEvents.GeneratorTests;

internal static class GeneratorTestHarness
{
    public const string GeneratedHintName = "SharedKernel.Messaging.IntegrationEvents.GeneratedIntegrationEvents.g.cs";

    private const string DefaultUsings = """
        using System;
        using SharedKernel.Domain;
        using SharedKernel.Messaging.IntegrationEvents;

        """;

    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return CSharpCompilation.Create(
            assemblyName: "SharedKernel.Messaging.IntegrationEvents.Tests.Dynamic",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        var syntaxTrees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(
            DefaultUsings + source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: $"Source{index:D4}.cs"));

        return CSharpCompilation.Create(
            assemblyName: "SharedKernel.Messaging.IntegrationEvents.Tests.Dynamic",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators) => RunGenerator(compilation, generators).RunResult;

    public static (GeneratorDriverRunResult RunResult, CSharpCompilation OutputCompilation) RunGenerator(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators)
    {
        var sourceGenerators = generators.Length == 0
            ? [new IntegrationEventMappingGenerator().AsSourceGenerator()]
            : generators.Select(static generator => generator.AsSourceGenerator()).ToArray();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: sourceGenerators,
            additionalTexts: [],
            parseOptions: (CSharpParseOptions?)compilation.SyntaxTrees.First().Options,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>.Empty));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return (driver.GetRunResult(), (CSharpCompilation)outputCompilation);
    }

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult)
    {
        var generatedSource = runResult.Results.SelectMany(static result => result.GeneratedSources).SingleOrDefault(
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

        references.Add(MetadataReference.CreateFromFile(typeof(IDomainEvent).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IIntegrationEvent).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location));
        return references;
    }
}
