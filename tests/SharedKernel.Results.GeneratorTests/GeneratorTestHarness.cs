using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Results.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Results.GeneratorTests;

/// <summary>
/// Results-specific wrapper over the shared Roslyn test harness that builds compilations referencing the Results assembly and runs the error catalog generator.
/// </summary>
internal static class GeneratorTestHarness
{
    private const string GeneratedHintName = "SharedKernel.Results.Generated.ResultErrorCatalog.g.cs";

    private const string DefaultUsings = """
        using System;
        using PandaBear.Results;

        """;

    /// <summary>
    /// Parses the source with default usings prepended and creates a compilation that references the Results assembly.
    /// </summary>
    /// <param name="source">The C# source to compile after the default using directives.</param>
    /// <param name="assemblyName">The name given to the compilation's assembly.</param>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Results.Tests.Dynamic")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation([syntaxTree], assemblyName, [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)]);
    }

    /// <summary>
    /// Runs the error catalog generator over the compilation and returns the driver's run result.
    /// </summary>
    /// <param name="compilation">The compilation the generator inspects.</param>
    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ResultErrorCatalogGenerator(), compilation);

    /// <summary>
    /// Returns the text of the generated error catalog file from the run result.
    /// </summary>
    /// <param name="runResult">The result of a generator driver run.</param>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);
}
