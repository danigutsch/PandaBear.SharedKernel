using System.Globalization;
using PandaBear.ConfigurationFiles;

namespace PandaBear.Sandbox.Tool;

/// <summary>Reads and validates the optional <c>.config/sharedkernel-sandbox.json</c>. Errors name the file and the JSON path.</summary>
internal static class SandboxConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-sandbox.json";

    private const int MaxPaths = 100;
    private const int MaxPathLength = 1024;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <returns>The configuration, or <see langword="null"/> when the file does not exist: the file is optional.</returns>
    /// <exception cref="InvalidOperationException">The file is larger than 1 MiB or not a valid configuration of version 1.</exception>
    public static async Task<SandboxConfiguration?> Read(string root, CancellationToken cancellationToken)
    {
        var configuration = await JsonConfigurationFile.ReadOptional(
            root, RelativePath, SandboxConfigurationJsonContext.Default.SandboxConfiguration, cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : Validate(configuration);
    }

    private static SandboxConfiguration Validate(SandboxConfiguration configuration)
    {
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
