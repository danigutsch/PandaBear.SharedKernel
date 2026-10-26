using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Mediator.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Immutable;

namespace PandaBear.Mediator.GeneratorTests;

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
        return CreateCompilation([source], assemblyName, additionalReferences, includeMediatorReference);
    }

    public static CSharpCompilation CreateCompilation(
        IEnumerable<string> sources,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic",
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        var syntaxTrees = sources
            .Select(static source => CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview)))
            .ToArray();
        IEnumerable<MetadataReference> mediator = includeMediatorReference
            ? [MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location)]
            : [];

        return SharedHarness.CreateCompilation(syntaxTrees, assemblyName, [.. mediator, .. additionalReferences ?? []]);
    }

    public static string RunGenerator(
        CSharpCompilation compilation,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        var runResult = RunGeneratorDriver(compilation);
        return GetGeneratedSource(runResult, hintName);
    }

    public static string GenerateSource(
        string source,
        string hintName = GeneratedHintNames.DiscoveryReport,
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic")
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReferences, includeMediatorReference);
        return RunGenerator(compilation, hintName);
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(
        CSharpCompilation compilation,
        ImmutableDictionary<string, string>? globalOptions = null)
    {
        return RunGeneratorDriver(compilation, globalOptions, trackIncrementalGeneratorSteps: false).RunResult;
    }

    public static (GeneratorDriver Driver, GeneratorDriverRunResult RunResult) RunGeneratorDriver(
        CSharpCompilation compilation,
        ImmutableDictionary<string, string>? globalOptions,
        bool trackIncrementalGeneratorSteps)
    {
        var driver = SharedHarness.CreateDriver(
            [new SharedKernelMediatorGenerator()],
            (CSharpParseOptions?)compilation.SyntaxTrees.FirstOrDefault()?.Options,
            globalOptions,
            trackIncrementalGeneratorSteps);

        driver = driver.RunGenerators(compilation);
        return (driver, driver.GetRunResult());
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(
        string source,
        IEnumerable<MetadataReference>? additionalReferences = null,
        ImmutableDictionary<string, string>? globalOptions = null,
        bool includeMediatorReference = true,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic")
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReferences, includeMediatorReference);
        return RunGeneratorDriver(compilation, globalOptions);
    }

    public static (GeneratorDriver Driver, GeneratorDriverRunResult RunResult) RunTrackedGeneratorDriver(
        GeneratorDriver driver,
        CSharpCompilation compilation)
    {
        var updatedDriver = driver.RunGenerators(compilation);
        return (updatedDriver, updatedDriver.GetRunResult());
    }

    public static (string Source, ImmutableArray<Diagnostic> Diagnostics) RunAndGetResult(
        CSharpCompilation compilation,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        var runResult = RunGeneratorDriver(compilation);
        return (GetGeneratedSource(runResult, hintName), runResult.Results.Single().Diagnostics);
    }

    public static string GetGeneratedSource(
        GeneratorDriverRunResult runResult,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        return SharedHarness.GetGeneratedSource(runResult, hintName);
    }

    public static MetadataReference CreateMetadataReference(
        string source,
        string assemblyName,
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReferences, includeMediatorReference);
        return MetadataReference.CreateFromImage(EmitToBytes(compilation));
    }

    public static Assembly LoadAssembly(CSharpCompilation compilation)
    {
        return new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true)
            .LoadFromStream(new MemoryStream(EmitToBytes(compilation)));
    }

    private static byte[] EmitToBytes(CSharpCompilation compilation)
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

        return stream.ToArray();
    }
}
