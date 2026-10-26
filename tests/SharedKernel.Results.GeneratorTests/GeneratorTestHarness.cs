using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Results.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Results.GeneratorTests;

internal static class GeneratorTestHarness
{
    private const string GeneratedHintName = "SharedKernel.Results.Generated.ResultErrorCatalog.g.cs";

    private const string DefaultUsings = """
        using System;
        using PandaBear.Results;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Results.Tests.Dynamic")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation([syntaxTree], assemblyName, [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)]);
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(CSharpCompilation compilation) =>
        SharedHarness.Run(new ResultErrorCatalogGenerator(), compilation);

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);
}
