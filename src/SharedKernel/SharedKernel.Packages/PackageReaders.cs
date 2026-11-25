using SharedKernel.Processes;

namespace SharedKernel.Packages;

/// <summary>The program and the reader for each package manager.</summary>
internal static class PackageReaders
{
    /// <summary>Returns the program that lists the manager's installed packages.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="manager"/> is not a known manager.</exception>
    public static string Executable(PackageManagerKind manager) => manager switch
    {
        PackageManagerKind.Apt => "dpkg-query",
        PackageManagerKind.Cargo => "cargo",
        PackageManagerKind.Npm => "npm",
        PackageManagerKind.Flatpak => "flatpak",
        PackageManagerKind.Snap => "snap",
        PackageManagerKind.Brew => "brew",
        _ => throw new ArgumentOutOfRangeException(nameof(manager)),
    };

    /// <summary>Creates the reader that runs the program at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="manager"/> is not a known manager.</exception>
    public static IPackageReader Create(PackageManagerKind manager, IProcessRunner runner, string path) => manager switch
    {
        PackageManagerKind.Apt => new AptPackageReader(runner, path),
        PackageManagerKind.Cargo => new CargoPackageReader(runner, path),
        PackageManagerKind.Npm => new NpmPackageReader(runner, path),
        PackageManagerKind.Flatpak => new FlatpakPackageReader(runner, path),
        PackageManagerKind.Snap => new SnapPackageReader(runner, path),
        PackageManagerKind.Brew => new BrewPackageReader(runner, path),
        _ => throw new ArgumentOutOfRangeException(nameof(manager)),
    };
}
