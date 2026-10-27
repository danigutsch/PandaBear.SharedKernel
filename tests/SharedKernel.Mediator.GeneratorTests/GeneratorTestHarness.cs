using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Mediator.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Immutable;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Builds compilations and runs SharedKernelMediatorGenerator over them for the mediator generator tests, and emits or loads the compiled result.</summary>
internal static class GeneratorTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    /// <summary>Compiles a single source at preview language version with the default usings and, unless excluded, a reference to the Mediator abstractions.</summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Mediator.Tests.Dynamic",
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        return CreateCompilation([source], assemblyName, additionalReferences, includeMediatorReference);
    }

    /// <summary>Compiles several sources at preview language version with the default usings prepended and the Mediator abstractions plus any additional references.</summary>
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

    /// <summary>Runs the generator over the compilation and returns the text of the generated source with the given hint name, which defaults to the discovery report.</summary>
    public static string RunGenerator(
        CSharpCompilation compilation,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        var runResult = RunGeneratorDriver(compilation);
        return GetGeneratedSource(runResult, hintName);
    }

    /// <summary>Compiles the source, runs the generator and returns the generated source with the given hint name, which defaults to the discovery report.</summary>
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

    /// <summary>Runs the generator once over the compilation with optional global analyzer options, without incremental step tracking, and returns the run result.</summary>
    public static GeneratorDriverRunResult RunGeneratorDriver(
        CSharpCompilation compilation,
        ImmutableDictionary<string, string>? globalOptions = null)
    {
        return RunGeneratorDriver(compilation, globalOptions, trackIncrementalGeneratorSteps: false).RunResult;
    }

    /// <summary>Runs the generator over the compilation with optional step tracking and returns both the driver, for later incremental runs, and the run result.</summary>
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

    /// <summary>Compiles the source and runs the generator over it, returning the run result.</summary>
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

    /// <summary>Runs an existing generator driver against a new compilation and returns the updated driver with its run result.</summary>
    public static (GeneratorDriver Driver, GeneratorDriverRunResult RunResult) RunTrackedGeneratorDriver(
        GeneratorDriver driver,
        CSharpCompilation compilation)
    {
        var updatedDriver = driver.RunGenerators(compilation);
        return (updatedDriver, updatedDriver.GetRunResult());
    }

    /// <summary>Runs the generator and returns the generated source with the given hint name together with the diagnostics of the single generator result.</summary>
    public static (string Source, ImmutableArray<Diagnostic> Diagnostics) RunAndGetResult(
        CSharpCompilation compilation,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        var runResult = RunGeneratorDriver(compilation);
        return (GetGeneratedSource(runResult, hintName), runResult.Results.Single().Diagnostics);
    }

    /// <summary>Returns the text of the generated source with the given hint name, which defaults to the discovery report.</summary>
    public static string GetGeneratedSource(
        GeneratorDriverRunResult runResult,
        string hintName = GeneratedHintNames.DiscoveryReport)
    {
        return SharedHarness.GetGeneratedSource(runResult, hintName);
    }

    /// <summary>Compiles and emits the source to bytes and returns an in-memory metadata reference to the resulting assembly.</summary>
    public static MetadataReference CreateMetadataReference(
        string source,
        string assemblyName,
        IEnumerable<MetadataReference>? additionalReferences = null,
        bool includeMediatorReference = true)
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReferences, includeMediatorReference);
        return MetadataReference.CreateFromImage(EmitToBytes(compilation));
    }

    /// <summary>Emits the compilation to memory and loads it into a new collectible AssemblyLoadContext.</summary>
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
