using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace PandaBear.Versioning.Tool;

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

    public static JsonObject ReadAndVerify(string path, string? feed = null)
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

            using var stream = File.OpenRead(Path.Combine(feed ?? Path.GetDirectoryName(path)!, name));
            var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(actual, value?.GetValue<string>(), StringComparison.Ordinal))
            {
                throw new ArgumentException($"Candidate package changed: {name}.");
            }
        }

        _ = SemanticVersion.Parse(evidence["packageVersion"]!.GetValue<string>());
        return evidence;
    }

    public static void Write(string path, JsonObject evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, evidence.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
