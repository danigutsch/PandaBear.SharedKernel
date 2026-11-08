namespace PandaBear.Packages;

/// <summary>A package manager that the library can read.</summary>
public enum PackageManagerKind
{
    /// <summary>Debian packages, read with <c>dpkg-query</c>.</summary>
    Apt,

    /// <summary>Rust crates installed with <c>cargo install</c>.</summary>
    Cargo,

    /// <summary>Global npm packages.</summary>
    Npm,

    /// <summary>Flatpak applications.</summary>
    Flatpak,
}
