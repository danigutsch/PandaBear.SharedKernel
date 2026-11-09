using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.Sandbox.Tool;

/// <summary>Reads and validates the optional <c>.config/sharedkernel-sandbox.json</c>. Errors name the file and the JSON path.</summary>
internal static class SandboxConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-sandbox.json";

    private const int MaxBytes = 1024 * 1024;
    private const int MaxPaths = 100;
    private const int MaxPathLength = 1024;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <returns>The configuration, or <see langword="null"/> when the file does not exist: the file is optional.</returns>
    /// <exception cref="InvalidOperationException">The file is larger than 1 MiB or not a valid configuration of version 1.</exception>
    public static async Task<SandboxConfiguration?> Read(string root, CancellationToken cancellationToken)
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

        if (!File.Exists(path))
        {
            return null;
        }

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

            return length > MaxBytes ? throw new InvalidOperationException($"{RelativePath} is larger than 1 MiB.") : Parse(buffer[..length]);
        }
    }

    private static SandboxConfiguration Parse(byte[] content)
    {
        SandboxConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, SandboxConfigurationJsonContext.Default.SandboxConfiguration);
        }
        catch (JsonException exception)
        {
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
            throw new InvalidOperationException($"{RelativePath}: $.version must be 1.");
        }

        if (configuration.DeniedReadPaths is null || configuration.DeniedReadPaths.Length > MaxPaths)
        {
            throw new InvalidOperationException($"{RelativePath}: $.deniedReadPaths must be a list of at most {MaxPaths} paths.");
        }

        for (var index = 0; index < configuration.DeniedReadPaths.Length; index++)
        {
            var entry = configuration.DeniedReadPaths[index];
            var valid = entry is { Length: > 0 and <= MaxPathLength }
                && !entry.Any(char.IsControl)
                && (entry.StartsWith("~/", StringComparison.Ordinal) || Path.IsPathFullyQualified(entry));
            if (!valid)
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{RelativePath}: $.deniedReadPaths[{index}] must be an absolute path or start with '~/'."));
            }
        }

        if (configuration.Settings is not null)
        {
            SettingsConfigurationValidator.Validate(configuration.Settings);
        }

        return configuration;
    }
}
