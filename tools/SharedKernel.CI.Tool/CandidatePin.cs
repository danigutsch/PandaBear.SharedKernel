using System.Text.Json;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// The identity of a candidate: the library revision it was built from, whether that checkout was dirty, the package
/// version, and the SHA-256 hash of each package. A candidate manifest also records evidence, which is not identity.
/// </summary>
/// <param name="LibraryRevision">The library commit, as 40 lowercase hexadecimal characters.</param>
/// <param name="LibraryDirty">Whether the library checkout had uncommitted changes; a pin requires <see langword="false"/>.</param>
/// <param name="PackageVersion">The version shared by every package.</param>
/// <param name="PackageSha256">The lowercase hexadecimal SHA-256 hash of each package, by file name.</param>
internal sealed partial record CandidatePin(
    string LibraryRevision,
    bool LibraryDirty,
    string PackageVersion,
    IReadOnlyDictionary<string, string> PackageSha256)
{
    /// <summary>The largest candidate manifest that is read.</summary>
    public const int MaxBytes = 1024 * 1024;

    private const string PackagePrefix = "PandaBear.SharedKernel.";

    /// <summary>Parses and validates a candidate manifest.</summary>
    /// <param name="json">The UTF-8 manifest.</param>
    /// <param name="source">Names the manifest at the start of every error message.</param>
    /// <returns>The validated identity.</returns>
    /// <exception cref="InvalidDataException">The manifest is not strict JSON or breaks an identity rule.</exception>
    public static CandidatePin Parse(ReadOnlySpan<byte> json, string source)
    {
        CandidatePin? pin;
        try
        {
            pin = JsonSerializer.Deserialize(json, CandidatePinJsonContext.Default.CandidatePin);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{source} is not a valid candidate manifest: {ControlCharacters.Escape(exception.Message)}", exception);
        }

        if (pin is null)
        {
            throw new InvalidDataException($"{source} is not a valid candidate manifest: it is null.");
        }

        pin.Validate(source);
        return pin;
    }

    /// <summary>Names each identity property whose value differs from <paramref name="other"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public string[] IdentityDifferences(CandidatePin other)
    {
        ArgumentNullException.ThrowIfNull(other);
        List<string> differences = [];
        if (!string.Equals(LibraryRevision, other.LibraryRevision, StringComparison.Ordinal))
        {
            differences.Add("libraryRevision");
        }

        if (LibraryDirty != other.LibraryDirty)
        {
            differences.Add("libraryDirty");
        }

        if (!string.Equals(PackageVersion, other.PackageVersion, StringComparison.Ordinal))
        {
            differences.Add("packageVersion");
        }

        if (PackageSha256.Count != other.PackageSha256.Count
            || PackageSha256.Any(package => !other.PackageSha256.TryGetValue(package.Key, out var hash) || !string.Equals(hash, package.Value, StringComparison.Ordinal)))
        {
            differences.Add("packageSha256");
        }

        return [.. differences];
    }

    private void Validate(string source)
    {
        if (!Revision().IsMatch(LibraryRevision))
        {
            throw new InvalidDataException($"{source}: libraryRevision must be 40 lowercase hexadecimal characters.");
        }

        if (LibraryDirty)
        {
            throw new InvalidDataException($"{source}: libraryDirty must be false, so the candidate comes from a committed revision.");
        }

        if (!Version().IsMatch(PackageVersion))
        {
            throw new InvalidDataException($"{source}: packageVersion may contain only letters, digits, '.', and '-'.");
        }

        if (PackageSha256.Count == 0)
        {
            throw new InvalidDataException($"{source}: packageSha256 must name at least one package.");
        }

        var suffix = "." + PackageVersion + ".nupkg";
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, hash) in PackageSha256.OrderBy(static package => package.Key, StringComparer.Ordinal))
        {
            if (!IsPackageName(name, suffix))
            {
                throw new InvalidDataException($"{source}: package {ControlCharacters.Escape(name)} does not match {PackagePrefix}<id>{suffix}.");
            }

            if (hash is null || !Sha256().IsMatch(hash))
            {
                throw new InvalidDataException($"{source}: the packageSha256 value of {name} must be 64 lowercase hexadecimal characters.");
            }

            if (!names.TryAdd(name, name))
            {
                throw new InvalidDataException($"{source}: packages {names[name]} and {name} differ only in case.");
            }
        }
    }

    // ^PandaBear[.]SharedKernel[.][A-Za-z0-9.]+[.]<version>[.]nupkg$, without building a pattern from the version.
    private static bool IsPackageName(string name, string suffix) =>
        name.Length > PackagePrefix.Length + suffix.Length
        && name.StartsWith(PackagePrefix, StringComparison.Ordinal)
        && name.EndsWith(suffix, StringComparison.Ordinal)
        && PackageId().IsMatch(name.AsSpan(PackagePrefix.Length, name.Length - PackagePrefix.Length - suffix.Length));

    [GeneratedRegex(@"\A[0-9a-f]{40}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Revision();

    [GeneratedRegex(@"\A[0-9A-Za-z.-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Version();

    [GeneratedRegex(@"\A[A-Za-z0-9.]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex PackageId();

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256();
}
