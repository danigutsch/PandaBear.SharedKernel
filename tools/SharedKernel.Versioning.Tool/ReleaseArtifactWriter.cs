using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Versioning.Tool;

internal static class ReleaseArtifactWriter
{
    private const string SbomNote = "No repository SBOM generator is currently wired into release prep.";

    public static async Task Write(PrepareReleaseOptions options, TextReader input)
    {
        if (!Directory.Exists(options.PackageDirectory))
        {
            throw new ArgumentException($"Package directory does not exist: {options.PackageDirectory}");
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var changes = FilterConventionalChanges(await input.ReadToEndAsync().ConfigureAwait(false));
        var releaseNotes = CreateReleaseNotes(options, changes);
        var changelog = "# Changelog" + Environment.NewLine + Environment.NewLine + releaseNotes;
        var manifest = CreateManifest(options);

        await File.WriteAllTextAsync(
            Path.Combine(options.OutputDirectory, "release-notes.md"),
            releaseNotes,
            Encoding.UTF8).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(options.OutputDirectory, "CHANGELOG.md"),
            changelog,
            Encoding.UTF8).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(options.OutputDirectory, "release-manifest.json"),
            manifest,
            Encoding.UTF8).ConfigureAwait(false);
    }

    private static string CreateReleaseNotes(PrepareReleaseOptions options, string changes)
    {
        var builder = new StringBuilder();
        builder.Append("# Release ").AppendLine(options.Version);
        builder.AppendLine();
        builder.Append("- Commit: `").Append(options.Sha ?? "unknown").AppendLine("`");
        if (string.IsNullOrWhiteSpace(options.SourceTag))
        {
            builder.AppendLine("- Previous release tag: none");
        }
        else
        {
            builder.Append("- Previous release tag: `").Append(options.SourceTag).AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(options.ReleaseImpact))
        {
            builder.Append("- Release impact: `").Append(options.ReleaseImpact).AppendLine("`");
        }

        builder.AppendLine();
        builder.AppendLine("## Changes");
        builder.AppendLine();
        builder.Append(string.IsNullOrWhiteSpace(changes) ? "- No commit summaries provided." : changes.TrimEnd());
        builder.AppendLine();
        return builder.ToString();
    }

    private static string CreateManifest(PrepareReleaseOptions options)
    {
        var packages = Directory.GetFiles(options.PackageDirectory, "*.nupkg")
            .Order(StringComparer.Ordinal)
            .Select(CreatePackageEntry)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("  \"version\": ").Append(Escape(options.Version)).AppendLine(",");
        builder.Append("  \"sourceSha\": ").Append(NullableEscape(options.Sha)).AppendLine(",");
        builder.Append("  \"sourceTag\": ").Append(NullableEscape(options.SourceTag)).AppendLine(",");
        builder.Append("  \"releaseImpact\": ").Append(NullableEscape(options.ReleaseImpact)).AppendLine(",");
        builder.AppendLine("  \"packages\": [");

        for (var index = 0; index < packages.Length; index++)
        {
            var package = packages[index];
            builder.AppendLine("    {");
            builder.Append("      \"fileName\": ").Append(Escape(package.FileName)).AppendLine(",");
            builder.Append("      \"sha256\": ").Append(Escape(package.Sha256)).AppendLine(",");
            builder.Append("      \"sizeBytes\": ").Append(package.SizeBytes).AppendLine();
            builder.Append(index == packages.Length - 1 ? "    }" : "    },");
            builder.AppendLine();
        }

        builder.AppendLine("  ],");
        builder.AppendLine("  \"sbom\": null,");
        builder.Append("  \"sbomNote\": ").Append(Escape(SbomNote)).AppendLine();
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string FilterConventionalChanges(string changes)
    {
        var lines = changes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var conventionalLines = lines
            .Where(IsConventionalChangeLine)
            .ToArray();

        return string.Join(Environment.NewLine, conventionalLines);
    }

    private static bool IsConventionalChangeLine(string line)
    {
        var subject = line.StartsWith("- ", StringComparison.Ordinal) ? line[2..] : line;
        return ConventionalCommitParser.TryParse(RemoveShortHashSuffix(subject), out _);
    }

    private static string RemoveShortHashSuffix(string subject)
    {
        var suffixStart = subject.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffixStart < 0 || !subject.EndsWith(')'))
        {
            return subject;
        }

        var suffix = subject[(suffixStart + 2)..^1];
        return suffix.Length is >= 7 and <= 12 && suffix.All(Uri.IsHexDigit)
            ? subject[..suffixStart]
            : subject;
    }

    private static ReleasePackageEntry CreatePackageEntry(string path)
    {
        var file = new FileInfo(path);
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return new ReleasePackageEntry(
            file.Name,
            Convert.ToHexString(hash),
            file.Length);
    }

    private static string NullableEscape(string? value) => string.IsNullOrWhiteSpace(value) ? "null" : Escape(value);

    private static string Escape(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
    }

    private sealed record ReleasePackageEntry(string FileName, string Sha256, long SizeBytes);
}
