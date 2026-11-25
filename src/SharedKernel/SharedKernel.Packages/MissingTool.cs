namespace SharedKernel.Packages;

/// <summary>A package manager with declared packages whose program was not found.</summary>
public sealed class MissingTool
{
    /// <summary>Creates the entry for a program that was not found.</summary>
    internal MissingTool(PackageManagerKind manager, string executable)
    {
        Manager = manager;
        Executable = executable;
    }

    /// <summary>Gets the package manager whose packages are declared.</summary>
    public PackageManagerKind Manager { get; }

    /// <summary>Gets the program name that was looked for, such as <c>dpkg-query</c> for apt.</summary>
    public string Executable { get; }
}
