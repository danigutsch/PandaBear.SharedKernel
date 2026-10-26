using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.BuildingBlocks;
using PandaBear.Domain.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Domain.GeneratorTests;

internal static class GeneratorTestHarness
{
    private const string DefaultUsings = """
        using PandaBear.BuildingBlocks;
        using PandaBear.Domain;

        """;

    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation(
            [syntaxTree],
            "SharedKernel.Domain.Tests.Dynamic",
            [MetadataReference.CreateFromFile(typeof(IIdentified<>).Assembly.Location)]);
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ModelIdentityGenerator(), compilation);

    public static Compilation RunGeneratorAndUpdateCompilation(
        CSharpCompilation compilation,
        out GeneratorDriverRunResult runResult)
    {
        runResult = SharedHarness.RunAndUpdate([new ModelIdentityGenerator()], compilation, out var outputCompilation, out _);
        return outputCompilation;
    }

    public static GeneratorDriverRunResult RunValueObjectGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ValueObjectGenerator(), compilation);

    public static Compilation RunValueObjectGeneratorAndUpdateCompilation(
        CSharpCompilation compilation,
        out GeneratorDriverRunResult runResult)
    {
        runResult = SharedHarness.RunAndUpdate([new ValueObjectGenerator()], compilation, out var outputCompilation, out _);
        return outputCompilation;
    }

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult, string hintName) =>
        SharedHarness.GetGeneratedSource(runResult, hintName);
}
