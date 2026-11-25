using Microsoft.CodeAnalysis;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Creates the code fix workspaces of the testing analyzer tests, whose documents use xUnit.</summary>
internal static class CodeFixTestWorkspace
{
    private const string DefaultUsings = CodeFixWorkspaceOptions.CommonUsings + "using Xunit;\n";

    private static readonly MetadataReference Xunit = MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);

    /// <summary>Creates a workspace with one Test0.cs document, optionally prefixed with the default usings, referencing all trusted platform assemblies and xUnit.</summary>
    /// <param name="source">C# source of the document.</param>
    /// <param name="assemblyName">Project and assembly name.</param>
    /// <param name="filePath">Path of the document, or null for a unique temporary path.</param>
    /// <param name="includeDefaultUsings">True to prepend the System, IO, threading, tasks and Xunit usings.</param>
    /// <param name="documentationMode">How XML documentation comments are parsed.</param>
    public static CodeFixWorkspace Create(
        string source,
        string assemblyName = "SharedKernel.Testing.CodeFixes.Tests.Dynamic",
        string? filePath = null,
        bool includeDefaultUsings = true,
        DocumentationMode documentationMode = DocumentationMode.Parse) =>
        CodeFixWorkspace.Create(source, new CodeFixWorkspaceOptions
        {
            AssemblyName = assemblyName,
            DefaultUsings = includeDefaultUsings ? DefaultUsings : string.Empty,
            AdditionalReferences = [Xunit],
            DocumentPath = filePath ?? Path.Combine(Path.GetTempPath(), "SharedKernel.Testing.CodeFixes.Tests", Guid.NewGuid().ToString("N"), "Test0.cs"),
            DocumentationMode = documentationMode,
        });
}
