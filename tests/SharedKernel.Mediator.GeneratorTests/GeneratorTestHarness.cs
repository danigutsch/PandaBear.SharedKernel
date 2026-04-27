using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Mediator.SourceGenerator;
using System.Reflection;
using System.Runtime.Loader;

namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic",
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        return CreateCompilation([DefaultUsings + source], assemblyName, additionalReferences, includeMediatorReference);
    }

    public static CSharpCompilation CreateCompilation(
        IEnumerable<string> sources,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic",
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        var syntaxTrees = sources
            .Select(static source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)))
            .ToArray();
        var references = GetMetadataReferences(additionalReferences, includeMediatorReference);

        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static string RunGenerator(
        CSharpCompilation compilation,
        string hintName = "SharedKernel.Mediator.Generated.DiscoveryReport.g.cs")
    {
        var runResult = RunGeneratorDriver(compilation);
        return GetGeneratedSource(runResult, hintName);
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation)
    {
        var generator = new SharedKernelMediatorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    public static string GetGeneratedSource(
        GeneratorDriverRunResult runResult,
        string hintName = "SharedKernel.Mediator.Generated.DiscoveryReport.g.cs")
    {
        var generatedSource = runResult.Results.Single().GeneratedSources.SingleOrDefault(
            source => string.Equals(source.HintName, hintName, StringComparison.Ordinal));

        if (generatedSource.SourceText is null)
        {
            throw new InvalidOperationException($"Generated source not found: {hintName}");
        }

        return generatedSource.SourceText.ToString();
    }

    public static MetadataReference CreateMetadataReference(
        string source,
        string assemblyName,
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReferences, includeMediatorReference);
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Failed to emit test compilation:{Environment.NewLine}{diagnostics}");
        }

        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static Assembly LoadAssembly(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Failed to emit test compilation:{Environment.NewLine}{diagnostics}");
        }

        stream.Position = 0;
        return new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true).LoadFromStream(stream);
    }

    private static List<MetadataReference> GetMetadataReferences(
        IEnumerable<MetadataReference>? additionalReferences,
        bool includeMediatorReference)
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        ArgumentException.ThrowIfNullOrEmpty(trustedPlatformAssemblies);

        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        if (includeMediatorReference)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location));
        }

        if (additionalReferences is not null)
        {
            references.AddRange(additionalReferences);
        }

        return references;
    }
}
