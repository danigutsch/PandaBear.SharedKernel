using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>An exact package version and the expected checksum of its artifact.</summary>
public sealed class PackagePin : ValueObject
{
    private const int ChecksumPrefixLength = 7;

    private PackagePin(PackageVersion version, string checksum)
    {
        Version = version;
        Checksum = checksum;
    }

    /// <summary>Gets the exact version of the artifact.</summary>
    public PackageVersion Version { get; }

    /// <summary>Gets the checksum as an algorithm prefix and lowercase hexadecimal digest.</summary>
    public string Checksum { get; }

    /// <summary>Creates a pin from a validated version and a hexadecimal SHA-256 or SHA-512 checksum.</summary>
    /// <param name="version">The exact version of the artifact.</param>
    /// <param name="checksum">The checksum with a <c>sha256:</c> or <c>sha512:</c> prefix.</param>
    /// <returns>The pin or a validation error that does not echo the input.</returns>
    public static Result<PackagePin> Create(PackageVersion? version, string? checksum)
    {
        if (version is null)
        {
            const string message = "A package pin requires a version.";
            return Result.Invalid<PackagePin>(message, nameof(version), message);
        }

        if (checksum is null || !IsValidChecksum(checksum))
        {
            const string message = "A package pin requires a sha256: or sha512: checksum with a hexadecimal digest of the correct length.";
            return Result.Invalid<PackagePin>(message, nameof(checksum), message);
        }

        var digest = Convert.FromHexString(checksum.AsSpan(ChecksumPrefixLength));
        return Result.Ok(new PackagePin(version, checksum[..ChecksumPrefixLength] + Convert.ToHexStringLower(digest)));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Version;
        yield return Checksum;
    }

    private static bool IsValidChecksum(string checksum)
    {
        var supported = checksum switch
        {
            { Length: ChecksumPrefixLength + 64 } => checksum.StartsWith("sha256:", StringComparison.Ordinal),
            { Length: ChecksumPrefixLength + 128 } => checksum.StartsWith("sha512:", StringComparison.Ordinal),
            _ => false,
        };

        return supported && checksum[ChecksumPrefixLength..].All(char.IsAsciiHexDigit);
    }
}
