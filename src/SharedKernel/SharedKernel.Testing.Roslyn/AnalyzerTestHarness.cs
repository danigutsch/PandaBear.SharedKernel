using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Testing.Roslyn;

/// <summary>
/// Creates Roslyn compilations for analyzer test suites.
/// </summary>
public static class AnalyzerTestHarness
{
    /// <summary>
    /// Creates a preview C# compilation with trusted-platform and caller-provided references.
    /// </summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string defaultUsings,
        IEnumerable<Assembly>? additionalReferenceAssemblies = null,
        string assemblyName = "SharedKernel.Testing.Roslyn.Dynamic",
        string path = "TestSource.cs")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(defaultUsings);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            defaultUsings + source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: path);

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            GetMetadataReferences(additionalReferenceAssemblies),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences(IEnumerable<Assembly>? additionalReferenceAssemblies)
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is required to create Roslyn test compilations.");
        }

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }

        foreach (var assembly in additionalReferenceAssemblies ?? [])
        {
            yield return MetadataReference.CreateFromFile(assembly.Location);
        }
    }
}
