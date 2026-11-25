using System.Text;
using SharedKernel.IO;
using SharedKernel.InputNormalization;

namespace SharedKernel.Repositories;

/// <summary>Compares write-once scaffold files to the current catalog without changing the repository.</summary>
public static class RepositorySeedDrift
{
    private const int MaxSeedBytes = 1024 * 1024;

    /// <summary>Reports each selected seed path and returns zero when every file matches.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="featureIds"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A render input or repository path is invalid, or a seed cannot be compared.</exception>
    public static async Task<int> Run(
        string rootPath,
        string? name,
        string? sdkVersion,
        IReadOnlyList<string> featureIds,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(featureIds);
        ArgumentNullException.ThrowIfNull(output);
        if (featureIds.Count == 0)
        {
            throw new InvalidOperationException("select at least one --feature to compare.");
        }

        HashSet<RepositoryFeature> features = [];
        foreach (var id in featureIds)
        {
            if (!RepositoryFeature.TryParse(id, out var feature) || !NewRepositorySeeds.Features.Contains(feature))
            {
                throw new InvalidOperationException(
                    $"unsupported seed feature '{ControlCharacters.Escape(id)}'. Choose from {string.Join(", ", NewRepositorySeeds.Features.Select(static candidate => candidate.Id))}.");
            }

            features.Add(feature);
        }

        SdkVersion? sdk = null;
        if (sdkVersion is not null && !SdkVersion.TryParse(sdkVersion, out sdk))
        {
            throw new InvalidOperationException(
                $"invalid --sdk-version '{ControlCharacters.Escape(sdkVersion)}'. Use major.minor.patch, such as 10.0.100, with a major version of 6 or higher.");
        }

        if (sdk is null && NewRepositorySeeds.Features.FirstOrDefault(feature => feature.NeedsSdkVersion && features.Contains(feature)) is { } needsSdk)
        {
            throw new InvalidOperationException($"feature '{needsSdk.Id}' needs --sdk-version, such as --sdk-version 10.0.100.");
        }

        var root = Path.GetFullPath(rootPath);
        var repositoryName = name ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        if ((name is not null || features.Contains(RepositoryFeature.Agents)) && !BaselineConfiguration.IsValidRepositoryName(repositoryName))
        {
            throw new InvalidOperationException("invalid --name. Use 1 to 100 letters, digits, '.', '-', or '_'.");
        }

        var hasDrift = false;
        foreach (var (path, expected) in NewRepositorySeeds.Render(repositoryName, features, sdk))
        {
            var actual = await ReadSeed(root, path, cancellationToken).ConfigureAwait(false);
            var status = actual switch
            {
                null => "missing",
                _ when actual == expected => "current",
                _ => "different",
            };
            hasDrift |= status != "current";
            await output.WriteLineAsync($"{status}: {path}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return hasDrift ? 1 : 0;
    }

    private static async Task<string?> ReadSeed(string root, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = RepositoryPath.Parse(path).ResolveUnder(root);
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"{path} is a directory.");
        }

        try
        {
            var bytes = await BoundedReader.ReadFile(fullPath, MaxSeedBytes, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{path} exceeds the 1 MiB limit.");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(RepositoryFile.WithoutUtf8ByteOrderMark(bytes).Span)
                .Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException($"{path} is not valid UTF-8.", exception);
        }
    }
}
