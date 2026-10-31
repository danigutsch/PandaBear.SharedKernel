using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tool;

/// <summary>Creates release candidate evidence and verifies that the candidate packages are consumable.</summary>
internal static class CandidateCommand
{
    /// <summary>Creates the candidate manifest, then consumes it into the same location.</summary>
    public static async Task Run(
        string root, string? version, string? baselineVersion, bool aot, TextWriter output, TextWriter error, TimeProvider timeProvider, CancellationToken ct)
    {
        var manifest = await Create(root, version, baselineVersion, output, timeProvider, ct).ConfigureAwait(false);
        await Consume(root, manifest, manifest, aot, output, error, ct).ConfigureAwait(false);
    }

    /// <summary>Requires a clean working tree, packs the candidate, and returns the path of the candidate manifest.</summary>
    /// <exception cref="ArgumentException">The working tree is not clean, or the baseline package mapping is not exclusively a local feed.</exception>
    public static async Task<string> Create(
        string root, string? version, string? baselineVersion, TextWriter output, TimeProvider timeProvider, CancellationToken ct)
    {
        if ((await CommandRunner.Run("git", ["status", "--porcelain"], root, ct).ConfigureAwait(false)).Length != 0)
        {
            throw new ArgumentException("Commit library changes before creating candidate evidence.");
        }

        if (baselineVersion is not null)
        {
            var config = XDocument.Load(Path.Combine(root, "NuGet.Config"));
            var mappings = config.Descendants("packageSource").Where(x => x.Elements("package").Any(p => (string?)p.Attribute("pattern") == "PandaBear.SharedKernel.*")).ToArray();
            if (mappings.Length != 1)
            {
                throw new ArgumentException("Baseline requires PandaBear.SharedKernel.* mapped exclusively to a local feed.");
            }

            var key = (string?)mappings[0].Attribute("key");
            var source = config.Descendants("packageSources").Elements("add").Single(x => (string?)x.Attribute("key") == key);
            var value = (string?)source.Attribute("value") ?? string.Empty;
            if (!Directory.Exists(Path.GetFullPath(value, root)) || config.Descendants("packageSource").Any(x => x != mappings[0] && x.Elements("package").Any(p => ((string?)p.Attribute("pattern"))?.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal) == true)))
            {
                throw new ArgumentException("Internal baseline packages must map exclusively to an existing local feed.");
            }
        }

        var revision = (await CommandRunner.Run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        var configured = XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("ComputedSemVer").First().Value;
        version ??= configured.Split('-')[0] + "-alpha.local.t" + timeProvider.GetUtcNow().ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture) + ".g" + revision[..12];
        await ApiCompatibilityCommand.Run(new ApiCompatibilityOptions(version, Path.Combine(root, "artifacts/api-compat"), "beta", root, baselineVersion, false), output, ct).ConfigureAwait(false);
        var feed = Path.Combine(root, "artifacts/api-compat/packages", version);
        var hashes = new JsonObject();
        foreach (var path in Directory.GetFiles(feed, "*.nupkg").Order(StringComparer.Ordinal))
        {
            hashes[Path.GetFileName(path)] = await CandidateManifest.HashPackage(path, ct).ConfigureAwait(false);
        }

        var evidence = new JsonObject
        {
            ["libraryRevision"] = revision,
            ["libraryDirty"] = false,
            ["packageVersion"] = version,
            ["packageSha256"] = hashes,
            ["consumerCompatibility"] = "pending",
            ["linuxPackageConsumption"] = "pending",
            ["windowsPackageConsumption"] = "pending",
            ["apiBaselineComparison"] = baselineVersion is null ? "pending" : "passed",
            ["apiValidation"] = "passed",
            ["stablePromotion"] = "pending"
        };
        var manifest = Path.Combine(feed, "candidate.json");
        CandidateManifest.Write(manifest, evidence);
        return manifest;
    }

    /// <summary>Verifies the manifest and installs and runs the listed packages on the current platform, recording the result in the evidence at <paramref name="destination"/>.</summary>
    public static async Task Consume(
        string root, string manifest, string destination, bool aot, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var evidence = CandidateManifest.ReadAndVerify(manifest);
        var version = evidence["packageVersion"]!.GetValue<string>();
        _ = SemanticVersion.Parse(version);
        var feed = Path.GetDirectoryName(manifest)!;
        RequireListedPackagesOnly(feed, evidence["packageSha256"]!.AsObject());
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";
        try
        {
            await PackageConsumptionCommand.Run(
                root, feed, version, aot, output, error, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A canceled run proves nothing, so it leaves the recorded evidence unchanged.
            evidence[platform] = "failed";
            CandidateManifest.Write(destination, evidence);
            throw;
        }

        evidence[platform] = "passed";
        if (aot)
        {
            evidence[OperatingSystem.IsWindows() ? "windowsNativeAot" : "linuxNativeAot"] = "passed";
        }

        CandidateManifest.Write(destination, evidence);
    }

    /// <summary>
    /// Requires the file name of every package in <paramref name="feed" /> to be a key of <paramref name="hashes" />,
    /// compared ordinally. Consumption installs every tool package in the directory and restores from it, and NuGet can
    /// restore an unlisted package instead of a listed one whose file name differs only in case.
    /// </summary>
    /// <exception cref="ArgumentException">The directory holds a package that the manifest does not list.</exception>
    private static void RequireListedPackagesOnly(string feed, JsonObject hashes)
    {
        var listed = hashes.Select(static hash => hash.Key).ToHashSet(StringComparer.Ordinal);
        var unlisted = Directory.GetFiles(feed, "*.nupkg")
            .Select(static package => Path.GetFileName(package))
            .Where(name => !listed.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unlisted.Length != 0)
        {
            throw new ArgumentException($"Candidate directory {feed} holds packages that the manifest does not list: {string.Join(", ", unlisted)}.");
        }
    }
}
