using System.Text.Json;
using SharedKernel.IO;

namespace SharedKernel.Repositories;

/// <summary>Matches restored assets to the SDK's current evaluation of a solution.</summary>
internal static class NuGetRestoreGraphReader
{
    private const int MaximumInputBytes = 64 * 1024 * 1024;

    /// <summary>Reads every evaluated project and reports missing or stale assets per project.</summary>
    internal static async Task<Dictionary<string, NuGetRestoreProject>> Read(
        string root, string specPath, CancellationToken cancellationToken)
    {
        using var specification = await Open(specPath, cancellationToken).ConfigureAwait(false);
        var expected = specification.RootElement.GetProperty("projects");
        var result = new Dictionary<string, NuGetRestoreProject>(StringComparer.Ordinal);
        foreach (var project in expected.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = Path.GetFullPath(project.Name, root);
            RollbackFileWriteBatch.ValidateWritePath(root, projectPath);
            var name = Path.GetRelativePath(root, projectPath).Replace('\\', '/');
            result.Add(name, await ReadProject(root, project.Value, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private static async Task<NuGetRestoreProject> ReadProject(
        string root, JsonElement specification, CancellationToken cancellationToken)
    {
        var restore = specification.GetProperty("restore");
        var lockExempt = restore.TryGetProperty("restoreLockProperties", out var locks)
            && locks.TryGetProperty("restorePackagesWithLockFile", out var withLock)
            && withLock.ValueKind == JsonValueKind.String
            && string.Equals(withLock.GetString(), "false", StringComparison.OrdinalIgnoreCase);

        try
        {
            var project = restore.GetProperty("projectPath").GetString();
            var output = restore.GetProperty("outputPath").GetString() ?? string.Empty;
            var assetsPath = Path.GetFullPath(Path.Combine(output, "project.assets.json"), root);
            var cachePath = Path.GetFullPath(Path.Combine(output, "project.nuget.cache"), root);
            RollbackFileWriteBatch.ValidateWritePath(root, assetsPath);
            RollbackFileWriteBatch.ValidateWritePath(root, cachePath);
            if (!File.Exists(assetsPath) || !File.Exists(cachePath))
            {
                return new(lockExempt, null, "Restore assets or cache are missing.");
            }

            using var assets = await Open(assetsPath, cancellationToken).ConfigureAwait(false);
            using var cache = await Open(cachePath, cancellationToken).ConfigureAwait(false);
            if (!cache.RootElement.GetProperty("success").GetBoolean()
                || !string.Equals(cache.RootElement.GetProperty("projectFilePath").GetString(), project, PathComparison)
                || !JsonElement.DeepEquals(assets.RootElement.GetProperty("project"), specification))
            {
                return new(lockExempt, null, "Restore inputs are stale or the latest restore failed.");
            }

            return new(lockExempt, NuGetRestoreGraph.Read(assets.RootElement), null) { Sources = ReadSources(restore) };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            or IOException or UnauthorizedAccessException or JsonException)
        {
            return new(lockExempt, null, "Restore evidence is malformed, outside the checkout, or unreadable.");
        }
    }

    private static NuGetVersionSources? ReadSources(JsonElement restore)
    {
        if (!restore.TryGetProperty("configFilePaths", out var files) || files.ValueKind != JsonValueKind.Array
            || !restore.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var config = files.EnumerateArray().Select(file => file.GetString() ?? string.Empty).ToArray();
        return new(config, sources.EnumerateObject().Select(source => source.Name).ToArray());
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static async Task<JsonDocument> Open(string path, CancellationToken cancellationToken)
    {
        var bytes = await BoundedReader.ReadFile(path, MaximumInputBytes, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Restore graph input is missing or exceeds the size limit.");
        return JsonDocument.Parse(RepositoryFile.WithoutUtf8ByteOrderMark(bytes));
    }
}
