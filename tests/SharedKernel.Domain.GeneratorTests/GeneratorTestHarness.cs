using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.BuildingBlocks;
using PandaBear.Domain.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Domain.GeneratorTests;

/// <summary>Builds compilations and runs the model identity and value object generators for the domain generator tests.</summary>
internal static class GeneratorTestHarness
{
    private const string DefaultUsings = """
        using PandaBear.BuildingBlocks;
        using PandaBear.Domain;

        """;

    /// <summary>Parses the source with the BuildingBlocks and Domain usings prepended at preview language version and compiles it with a reference to the BuildingBlocks assembly.</summary>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation(
            [syntaxTree],
            "SharedKernel.Domain.Tests.Dynamic",
            [MetadataReference.CreateFromFile(typeof(IIdentified<>).Assembly.Location)]);
    }

    /// <summary>Runs ModelIdentityGenerator over the compilation and returns the driver result.</summary>
    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ModelIdentityGenerator(), compilation);

    /// <summary>Runs ModelIdentityGenerator, outputs its run result, and returns the compilation updated with the generated sources.</summary>
    public static Compilation RunGeneratorAndUpdateCompilation(
        CSharpCompilation compilation,
        out GeneratorDriverRunResult runResult)
    {
        runResult = SharedHarness.RunAndUpdate([new ModelIdentityGenerator()], compilation, out var outputCompilation, out _);
        return outputCompilation;
    }

    /// <summary>Runs ValueObjectGenerator over the compilation and returns the driver result.</summary>
    public static GeneratorDriverRunResult RunValueObjectGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ValueObjectGenerator(), compilation);

    /// <summary>Runs ValueObjectGenerator, outputs its run result, and returns the compilation updated with the generated sources.</summary>
    public static Compilation RunValueObjectGeneratorAndUpdateCompilation(
        CSharpCompilation compilation,
        out GeneratorDriverRunResult runResult)
    {
        runResult = SharedHarness.RunAndUpdate([new ValueObjectGenerator()], compilation, out var outputCompilation, out _);
        return outputCompilation;
    }

    /// <summary>Returns the text of the generated source file with the given hint name from the run result.</summary>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult, string hintName) =>
        SharedHarness.GetGeneratedSource(runResult, hintName);
}
