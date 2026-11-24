using System.Text.Json;
using PandaBear.IO;

namespace PandaBear.Repositories;

/// <summary>Reads applicable verification results from the existing CI execution manifest format.</summary>
internal static class NuGetRestoreVerificationReader
{
    /// <summary>Reads verification for an exact clean revision.</summary>
    /// <exception cref="InvalidDataException">The evidence belongs to another revision, or is missing or too large.</exception>
    internal static async Task<NuGetRestoreVerification?> Read(
        string root, string? receipt, string revision, bool dirty, CancellationToken cancellationToken)
    {
        if (receipt is null)
        {
            return null;
        }

        var path = Path.GetFullPath(receipt, root);
        RollbackFileWriteBatch.ValidateWritePath(root, path);
        var bytes = await BoundedReader.ReadFile(path, 4 * 1024 * 1024, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The verification receipt is missing or exceeds the size limit.");
        using var document = JsonDocument.Parse(RepositoryFile.WithoutUtf8ByteOrderMark(bytes));
        var manifest = document.RootElement;
        if (dirty || !string.Equals(manifest.GetProperty("revision").GetString(), revision, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Verification must apply to the exact clean checkout.");
        }

        var phases = manifest.GetProperty("phases").EnumerateArray()
            .Where(phase => phase.GetProperty("name").GetString() is "restore" or "build" or "test")
            .ToDictionary(phase => phase.GetProperty("name").GetString()!, phase => ReadStatus(phase), StringComparer.Ordinal);
        var slices = manifest.GetProperty("slices").EnumerateArray().Select(slice => slice.GetString()!)
            .Order(StringComparer.Ordinal).ToArray();
        return new(ReadStatus(manifest), slices, phases.GetValueOrDefault("restore"), phases.GetValueOrDefault("build"), phases.GetValueOrDefault("test"));
    }

    private static string ReadStatus(JsonElement element)
    {
        var status = element.GetProperty("status").GetString();
        return status is "passed" or "failed" or "canceled" or "skipped"
            ? status
            : throw new InvalidDataException("The verification status is unknown.");
    }
}
