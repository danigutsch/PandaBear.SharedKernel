using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Maps a package manager to the ecosystem name that OSV (osv.dev) uses.</summary>
public static class OsvEcosystems
{
    /// <summary>Returns the OSV ecosystem of <paramref name="manager"/>.</summary>
    /// <param name="manager">The package manager.</param>
    /// <param name="aptEcosystem">
    /// For apt only: the distribution release as OSV names it, such as <c>Debian:12</c> or <c>Ubuntu:24.04:LTS</c>. OSV
    /// keeps Debian and Ubuntu advisories per release, so there is no usable default.
    /// </param>
    /// <returns>
    /// The ecosystem, or an <c>Invalid</c> failure for apt without a valid release and for a manager that OSV has no
    /// ecosystem for (Flatpak and any other): an unsupported manager is an error, never an empty answer.
    /// </returns>
    public static Result<string> For(PackageManagerKind manager, string? aptEcosystem = null) => manager switch
    {
        PackageManagerKind.Cargo => Result.Ok("crates.io"),
        PackageManagerKind.Npm => Result.Ok("npm"),
        PackageManagerKind.Apt => IsRelease(aptEcosystem)
            ? Result.Ok(aptEcosystem!)
            : Result.Invalid<string>(
                "apt needs an OSV release such as Debian:12 or Ubuntu:24.04:LTS.",
                "aptEcosystem",
                "apt needs an OSV release such as Debian:12 or Ubuntu:24.04:LTS."),
        _ => Result.Invalid<string>(
            "OSV has no ecosystem for this package manager.",
            "manager",
            "OSV has no ecosystem for this package manager."),
    };

    // Debian:<release> or Ubuntu:<release>[:LTS], optionally Ubuntu:Pro:<release>[:LTS]; 2 to 4 segments of letters, digits, dots.
    private static bool IsRelease(string? value)
    {
        if (value is null or { Length: > 64 })
        {
            return false;
        }

        var parts = value.Split(':');
        return parts is { Length: >= 2 and <= 4 }
            && parts[0] is "Debian" or "Ubuntu"
            && parts.Skip(1).All(static p => p.Length > 0 && p.All(static c => char.IsAsciiLetterOrDigit(c) || c == '.'))
            && (parts[0] == "Ubuntu" || parts.Length == 2);
    }
}
