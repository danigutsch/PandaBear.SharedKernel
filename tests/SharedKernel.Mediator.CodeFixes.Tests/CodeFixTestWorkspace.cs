using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Mediator.SourceGenerator;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Mediator.CodeFixes.Tests;

/// <summary>Creates the code fix workspaces of the mediator code fix tests and runs the mediator generator over them.</summary>
internal static class CodeFixTestWorkspace
{
    private const string DynamicAssemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Dynamic";

    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    private static readonly MetadataReference Abstractions = MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location);

    /// <summary>Returns the options of a mediator test project with the given assembly name and nullable context.</summary>
    /// <param name="assemblyName">The project and assembly name.</param>
    /// <param name="nullable">The project-wide nullable context.</param>
    private static CodeFixWorkspaceOptions Options(string assemblyName, NullableContextOptions nullable = NullableContextOptions.Disable) => new()
    {
        AssemblyName = assemblyName,
        DefaultUsings = DefaultUsings,
        AdditionalReferences = [Abstractions],
        NullableContextOptions = nullable,
    };

    /// <summary>Creates a workspace containing a single C# document.</summary>
    /// <param name="source">The source to place in the primary document.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The initialized test workspace.</returns>
    public static CodeFixWorkspace Create(string source, string assemblyName = DynamicAssemblyName) =>
        CodeFixWorkspace.Create(source, Options(assemblyName));

    /// <summary>Creates a workspace whose primary document relies on a companion <c>GlobalUsings.cs</c> file.</summary>
    /// <param name="source">The source to place in the primary document.</param>
    /// <param name="globalUsingsSource">The source to place in the global-usings document.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The initialized test workspace.</returns>
    public static CodeFixWorkspace CreateWithGlobalUsings(string source, string globalUsingsSource, string assemblyName = DynamicAssemblyName) =>
        CodeFixWorkspace.CreateWithGlobalUsings(source, globalUsingsSource, Options(assemblyName));

    /// <summary>Creates a workspace with nullable reference types enabled at the project level.</summary>
    /// <param name="source">The source to place in the primary document.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The initialized test workspace.</returns>
    public static CodeFixWorkspace CreateWithNullableEnabled(string source, string assemblyName = DynamicAssemblyName) =>
        CodeFixWorkspace.Create(source, Options(assemblyName, NullableContextOptions.Enable));

    /// <summary>Creates a two-project workspace with the primary project referencing a module project.</summary>
    /// <param name="primarySource">The source placed in the primary project.</param>
    /// <param name="moduleSource">The source placed in the referenced module project.</param>
    /// <param name="primaryAssemblyName">The dynamic primary assembly name.</param>
    /// <param name="moduleAssemblyName">The dynamic module assembly name.</param>
    /// <returns>The initialized multi-project workspace.</returns>
    public static CodeFixWorkspace CreateWithProjectReference(
        string primarySource,
        string moduleSource,
        string primaryAssemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Primary",
        string moduleAssemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Module") =>
        CodeFixWorkspace.CreateWithProjectReference(primarySource, moduleSource, moduleAssemblyName, Options(primaryAssemblyName));

    /// <summary>Gets all mediator generator diagnostics for the current state of the primary project.</summary>
    /// <param name="workspace">The workspace whose primary project the generator runs over.</param>
    /// <returns>The diagnostics emitted by the mediator source generator.</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetGeneratorDiagnostics(this CodeFixWorkspace workspace) =>
        (await RunGenerator(workspace).ConfigureAwait(false)).Diagnostics;

    /// <summary>Gets the single mediator generator diagnostic with the requested identifier.</summary>
    /// <param name="workspace">The workspace whose primary project the generator runs over.</param>
    /// <param name="diagnosticId">The diagnostic identifier to locate.</param>
    /// <returns>The matching diagnostic.</returns>
    public static async Task<Diagnostic> GetSingleGeneratorDiagnostic(this CodeFixWorkspace workspace, string diagnosticId)
    {
        var diagnostics = await workspace.GetGeneratorDiagnostics().ConfigureAwait(false);
        return diagnostics.ShouldHaveSingleItem(diagnostic => diagnostic.Id == diagnosticId);
    }

    /// <summary>Gets a source that the mediator generator generates for the primary project, by hint name.</summary>
    /// <param name="workspace">The workspace whose primary project the generator runs over.</param>
    /// <param name="hintName">The generated source hint name.</param>
    /// <returns>The generated source text.</returns>
    public static async Task<string> GetGeneratedSource(this CodeFixWorkspace workspace, string hintName)
    {
        var result = await RunGenerator(workspace).ConfigureAwait(false);
        var generatedSource = result.GeneratedSources.ShouldHaveSingleItem(candidate => string.Equals(candidate.HintName, hintName, StringComparison.Ordinal));
        return generatedSource.SourceText.ToString();
    }

    private static async Task<GeneratorRunResult> RunGenerator(CodeFixWorkspace workspace)
    {
        var compilation = (CSharpCompilation?)await workspace.Project.GetCompilationAsync().ConfigureAwait(false);
        _ = compilation.ShouldNotBeNull();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MediatorGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Results.Single();
    }
}
