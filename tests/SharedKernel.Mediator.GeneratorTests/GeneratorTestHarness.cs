using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Mediator.SourceGenerator;

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
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = GetMetadataReferences(additionalReferences, includeMediatorReference);

        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static string RunGenerator(CSharpCompilation compilation)
    {
        var generator = new SharedKernelMediatorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        return runResult.Results.Single().GeneratedSources.Single().SourceText.ToString();
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
