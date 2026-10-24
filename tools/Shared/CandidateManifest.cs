using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace PandaBear.Tooling;

/// <summary>
/// Finds, reads, and writes <c>candidate.json</c>, which records a candidate's library revision, package version, and
/// the SHA-256 hash of each package.
/// </summary>
internal static class CandidateManifest
{
    public static string Find(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException($"Candidate directory does not exist: {directory}.");
        }

        var manifests = Directory.EnumerateFiles(directory, "candidate.json", SearchOption.AllDirectories).Take(2).ToArray();
        return manifests.Length == 1
            ? manifests[0]
            : throw new ArgumentException($"Candidate directory must contain exactly one candidate.json, searched recursively: {directory}.");
    }

    public static JsonObject ReadAndVerify(string path)
    {
        var evidence = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new ArgumentException("Candidate manifest is empty.");
        var hashes = evidence["packageSha256"]?.AsObject()
            ?? throw new ArgumentException("Candidate package hashes are unavailable.");
        if (hashes.Count == 0)
        {
            throw new ArgumentException("Candidate package hashes are unavailable.");
        }

        foreach (var (name, value) in hashes)
        {
            if (name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || Path.GetFileName(name) != name || !name.EndsWith(".nupkg", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid candidate package filename: {name}.");
            }

            using var stream = File.OpenRead(Path.Combine(Path.GetDirectoryName(path)!, name));
            var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(actual, value?.GetValue<string>(), StringComparison.Ordinal))
            {
                throw new ArgumentException($"Candidate package changed: {name}.");
            }
        }

        return evidence;
    }

    /// <summary>Returns the SHA-256 hash of a package file as the lowercase hexadecimal text that <c>packageSha256</c> records.</summary>
    public static async Task<string> HashPackage(string path, CancellationToken ct)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Replaces <paramref name="path" /> through a temporary file in the same directory, so an interrupted write never
    /// leaves a truncated manifest.
    /// </summary>
    public static void Write(string path, JsonObject evidence)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, evidence.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
