using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Testing.SourceGenerator;
using SharedHarness = SharedKernel.Testing.Roslyn.GeneratorTestHarness;

namespace SharedKernel.Testing.GeneratorTests;

/// <summary>Builds compilations that reference SharedKernel.Testing and runs the CallProxy generator over them.</summary>
internal static class GeneratorTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using SharedKernel.Testing;

        """;

    /// <summary>Parses the source with the default usings prepended and compiles it, nullable enabled, with references to SharedKernel.Testing and dependency injection.</summary>
    /// <param name="source">The C# source after the default using directives.</param>
    /// <param name="languageVersion">The C# version; C# 14 unless a test needs another.</param>
    /// <returns>The compilation.</returns>
    public static CSharpCompilation CreateCompilation(string source, LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(languageVersion), "Source.cs");

        var compilation = SharedHarness.CreateCompilation(
            [syntaxTree],
            "SharedKernel.Testing.Tests.Dynamic",
            [
                MetadataReference.CreateFromFile(typeof(CallProxy).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            ]);
        // Like the SDK, which suppresses the CS1701 and CS1702 assembly unification warnings by default.
        return compilation.WithOptions(compilation.Options
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithSpecificDiagnosticOptions(compilation.Options.SpecificDiagnosticOptions
                .SetItem("CS1701", ReportDiagnostic.Suppress)
                .SetItem("CS1702", ReportDiagnostic.Suppress)));
    }

    /// <summary>Runs the CallProxy generator over the compilation.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The run result.</returns>
    public static GeneratorDriverRunResult Run(CSharpCompilation compilation) =>
        SharedHarness.Run(new CallProxyGenerator(), compilation);

    /// <summary>Runs the CallProxy generator and returns the errors and warnings of the compilation that includes the generated sources.</summary>
    /// <param name="source">The C# source after the default using directives.</param>
    /// <param name="runResult">The run result.</param>
    /// <returns>The compiler errors and warnings.</returns>
    public static ImmutableArray<Diagnostic> Compile(string source, out GeneratorDriverRunResult runResult)
    {
        runResult = SharedHarness.RunAndUpdate([new CallProxyGenerator()], CreateCompilation(source), out var output, out _);
        return [.. output.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)];
    }

    /// <summary>Returns the hint names of the generated sources.</summary>
    /// <param name="runResult">The run result.</param>
    /// <returns>The hint names, in generation order.</returns>
    public static string[] GetHintNames(GeneratorDriverRunResult runResult) =>
        [.. runResult.Results.SelectMany(static result => result.GeneratedSources).Select(static source => source.HintName)];

    /// <summary>Returns the text of one generated source.</summary>
    /// <param name="runResult">The run result.</param>
    /// <param name="hintName">The hint name.</param>
    /// <returns>The generated source text.</returns>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult, string hintName) =>
        SharedHarness.GetGeneratedSource(runResult, hintName);

    /// <summary>Returns the diagnostics the generator reported.</summary>
    /// <param name="runResult">The run result.</param>
    /// <returns>The diagnostics.</returns>
    public static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetDiagnostics(runResult);

    /// <summary>Returns the source text a diagnostic points at.</summary>
    /// <param name="compilation">The compilation the diagnostic was reported for.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The text of its span in the parsed source.</returns>
    public static string GetLocatedText(CSharpCompilation compilation, Diagnostic diagnostic) =>
        compilation.SyntaxTrees[0].GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan);
}
