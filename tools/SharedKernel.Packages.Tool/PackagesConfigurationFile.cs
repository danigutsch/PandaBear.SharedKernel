using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.Packages.Tool;

/// <summary>
/// Reads and validates <c>.config/sharedkernel-packages.json</c> into <see cref="DeclaredPackages"/>. Errors name the
/// file and the JSON path of the problem.
/// </summary>
internal static class PackagesConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-packages.json";

    private const int MaxBytes = 1024 * 1024;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, larger than 1 MiB, or not a valid configuration of version 1, a manager ID or an entry is
    /// invalid, or a package has more than one owner.
    /// </exception>
    public static async Task<DeclaredPackages> Read(string root, CancellationToken cancellationToken) =>
        (await ReadWithPolicy(root, cancellationToken).ConfigureAwait(false)).Packages;

    /// <summary>Reads the configuration like <see cref="Read"/> and also returns the optional <c>minimumReleaseAgeDays</c>.</summary>
    /// <exception cref="InvalidOperationException">As <see cref="Read"/>, or <c>minimumReleaseAgeDays</c> is outside 0 to 3650.</exception>
    public static async Task<(DeclaredPackages Packages, int? MinimumReleaseAgeDays)> ReadWithPolicy(string root, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = RepositoryPath.Resolve(root, RelativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{RelativePath}: {exception.Message}", exception);
        }

        byte[] content;
        try
        {
            content = await ReadBounded(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{RelativePath} does not exist.", exception);
        }

        return Parse(content);
    }

    private static (DeclaredPackages Packages, int? MinimumReleaseAgeDays) Parse(byte[] content)
    {
        PackagesConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, PackagesConfigurationJsonContext.Default.PackagesConfiguration);
        }
        catch (JsonException exception)
        {
            // The serializer's message names .NET types, so only the location is reported.
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{RelativePath} is not valid at {exception.Path ?? "$"} (line {(exception.LineNumber ?? 0) + 1}). Check for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (configuration is null)
        {
            throw new InvalidOperationException($"{RelativePath} must contain a JSON object.");
        }

        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (configuration.MinimumReleaseAgeDays is { } days && MinimumReleaseAge.FromDays(days).IsFailure)
        {
            throw Invalid($"$.minimumReleaseAgeDays must be from 0 to {MinimumReleaseAge.MaxDays}.");
        }

        Dictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>> declared = [];
        foreach (var (id, entries) in configuration.Packages)
        {
            if (!PackageManagers.TryParse(id, out var manager))
            {
                throw Invalid($"$.packages.{id} is not a known package manager; the managers are: {string.Join(", ", PackageManagers.Ids)}.");
            }

            // RespectNullableAnnotations does not cover dictionary values or array elements.
            if (entries is null)
            {
                throw Invalid($"$.packages.{id} must be a list of package names, not null.");
            }

            List<PackageRequirement> requirements = [];
            for (var index = 0; index < entries.Length; index++)
            {
                requirements.Add(ParseEntry(id, index, entries[index]));
            }

            declared[manager.Value] = requirements;
        }

        var result = DeclaredPackages.Create(declared);
        if (result.IsFailure)
        {
            var messages = result.ErrorDetails?.ValidationErrors?.Values.SelectMany(static v => v) ?? [];
            throw Invalid("$.packages gives a package more than one owner:" + string.Concat(messages.Select(static m => "\n- " + m)));
        }

        return (result.Value, configuration.MinimumReleaseAgeDays);
    }

    private static PackageRequirement ParseEntry(string manager, int index, string? entry)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"$.packages.{manager}[{index}]");
        if (entry is null)
        {
            throw Invalid($"{path} must be a package name, not null.");
        }

        var separator = entry.IndexOf('=', StringComparison.Ordinal);
        var name = PackageName.Create(separator < 0 ? entry : entry[..separator]);
        if (!name.IsSuccess)
        {
            throw Invalid($"{path} must start with a valid package name.");
        }

        if (separator < 0)
        {
            return new PackageRequirement(name.Value);
        }

        var version = PackageVersion.Create(entry[(separator + 1)..]);
        return version.IsSuccess ? new PackageRequirement(name.Value, version.Value) : throw Invalid($"{path} must have a valid version after '='.");
    }

    private static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    private static async Task<byte[]> ReadBounded(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            return length > MaxBytes ? throw new InvalidOperationException($"{RelativePath} is larger than 1 MiB.") : buffer[..length];
        }
    }
}
