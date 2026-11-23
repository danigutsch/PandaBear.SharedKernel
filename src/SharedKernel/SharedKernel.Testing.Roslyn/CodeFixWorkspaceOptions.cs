using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Roslyn;

/// <summary>How a <see cref="CodeFixWorkspace"/> builds its project and documents.</summary>
public sealed class CodeFixWorkspaceOptions
{
    /// <summary>The <c>using</c> directives that <see cref="DefaultUsings"/> holds unless a test sets its own.</summary>
    public const string CommonUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    /// <summary>Gets the project and assembly name. The default is <c>CodeFixWorkspace.Dynamic</c>.</summary>
    public string AssemblyName { get; init; } = "CodeFixWorkspace.Dynamic";

    /// <summary>Gets the text put before the source of every document the workspace creates; empty for none. The default is <see cref="CommonUsings"/>.</summary>
    public string DefaultUsings { get; init; } = CommonUsings;

    /// <summary>Gets the references that the project has besides the trusted platform assemblies of the test process.</summary>
    public IReadOnlyList<MetadataReference> AdditionalReferences { get; init; } = [];

    /// <summary>Gets the path of the main document <c>Test0.cs</c>; the project file and output lie in its directory. The default is <c>/Test0.cs</c>.</summary>
    public string DocumentPath { get; init; } = "/Test0.cs";

    /// <summary>Gets how documentation comments are parsed. The default is <see cref="DocumentationMode.Parse"/>.</summary>
    public DocumentationMode DocumentationMode { get; init; } = DocumentationMode.Parse;

    /// <summary>Gets the project-wide nullable context. The default is <see cref="NullableContextOptions.Disable"/>.</summary>
    public NullableContextOptions NullableContextOptions { get; init; } = NullableContextOptions.Disable;
}
