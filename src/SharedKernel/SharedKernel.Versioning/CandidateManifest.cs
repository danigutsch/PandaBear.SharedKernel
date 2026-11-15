using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.Versioning;

/// <summary>
/// The <c>candidate.json</c> of a package candidate: its library revision, package version, the SHA-256 hash of each
/// package file beside it, and the evidence that validation and consumption add.
/// </summary>
public sealed class CandidateManifest
{
    /// <summary>The file name of a candidate manifest.</summary>
    public const string FileName = "candidate.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Creates a manifest from its JSON content.</summary>
    /// <param name="evidence">The manifest content, which this instance owns and updates.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evidence"/> is <see langword="null"/>.</exception>
    public CandidateManifest(JsonObject evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence;
    }

    /// <summary>Gets the manifest content. Callers add evidence to it before <see cref="Save"/>.</summary>
    public JsonObject Evidence { get; }

    /// <summary>Returns the path of the single <c>candidate.json</c> found recursively under the directory.</summary>
    /// <param name="directory">Directory to search.</param>
    /// <exception cref="ArgumentException">The directory does not exist, or does not contain exactly one <c>candidate.json</c>.</exception>
    public static string Find(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException($"Candidate directory does not exist: {directory}.");
        }

        var manifests = Directory.EnumerateFiles(directory, FileName, SearchOption.AllDirectories).Take(2).ToArray();
        return manifests.Length == 1
            ? manifests[0]
            : throw new ArgumentException($"Candidate directory must contain exactly one candidate.json, searched recursively: {directory}.");
    }

    /// <summary>Reads a manifest and verifies that every package it lists is beside it, unchanged.</summary>
    /// <param name="path">Path of the <c>candidate.json</c> file.</param>
    /// <exception cref="ArgumentException">The manifest is empty, lacks package hashes, names an invalid package file name, or a package changed.</exception>
    public static CandidateManifest Load(string path)
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

        return new CandidateManifest(evidence);
    }

    /// <summary>Returns the SHA-256 hash of a package file as the lowercase hexadecimal text that <c>packageSha256</c> records.</summary>
    /// <param name="path">Path of the package file.</param>
    /// <param name="ct">Cancels reading the file.</param>
    public static async Task<string> HashPackage(string path, CancellationToken ct)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Writes the manifest to <paramref name="path" /> through a temporary file in the same directory, so an interrupted
    /// write never leaves a truncated manifest.
    /// </summary>
    /// <param name="path">Path of the <c>candidate.json</c> file to replace.</param>
    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, Evidence.ToJsonString(WriteOptions) + Environment.NewLine);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
