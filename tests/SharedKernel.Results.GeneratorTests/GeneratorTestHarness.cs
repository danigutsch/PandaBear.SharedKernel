using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Results.SourceGenerator;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Results.GeneratorTests;

internal static class GeneratorTestHarness
{
    private const string GeneratedHintName = "SharedKernel.Results.Generated.ResultErrorCatalog.g.cs";

    private const string DefaultUsings = """
        using System;
        using SharedKernel.Results;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Results.Tests.Dynamic")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = GetMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation)
    {
        var generator = new ResultErrorCatalogGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: [],
            parseOptions: (CSharpParseOptions?)compilation.SyntaxTrees.First().Options,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>.Empty));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
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

        references.Add(MetadataReference.CreateFromFile(typeof(Result).Assembly.Location));
        return references;
    }
}
