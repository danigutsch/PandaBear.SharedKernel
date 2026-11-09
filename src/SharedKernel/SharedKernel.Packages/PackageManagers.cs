using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Packages;

/// <summary>The stable text IDs of the package managers, as they appear in configuration and messages.</summary>
public static class PackageManagers
{
    /// <summary>Gets the IDs of all managers, in enum order.</summary>
    public static IReadOnlyList<string> Ids { get; } = ["apt", "cargo", "npm", "flatpak"];

    /// <summary>Returns the ID of <paramref name="manager"/>.</summary>
    /// <param name="manager">The package manager.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="manager"/> is not a defined value.</exception>
    public static string Id(PackageManagerKind manager) => manager switch
    {
        PackageManagerKind.Apt => "apt",
        PackageManagerKind.Cargo => "cargo",
        PackageManagerKind.Npm => "npm",
        PackageManagerKind.Flatpak => "flatpak",
        _ => throw new ArgumentOutOfRangeException(nameof(manager)),
    };

    /// <summary>Looks up the manager with the ID <paramref name="id"/>, compared exactly.</summary>
    /// <param name="id">The ID, such as <c>apt</c>.</param>
    /// <param name="manager">The manager when found.</param>
    /// <returns><see langword="false"/> when no manager has that ID.</returns>
    public static bool TryParse(string? id, [NotNullWhen(true)] out PackageManagerKind? manager)
    {
        manager = id switch
        {
            "apt" => PackageManagerKind.Apt,
            "cargo" => PackageManagerKind.Cargo,
            "npm" => PackageManagerKind.Npm,
            "flatpak" => PackageManagerKind.Flatpak,
            _ => null,
        };
        return manager is not null;
    }
}
