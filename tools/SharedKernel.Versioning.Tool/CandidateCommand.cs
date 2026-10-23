using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace PandaBear.Versioning.Tool;

internal static class CandidateCommand
{
    public static async Task Run(
        string root, string? version, string? baselineVersion, bool aot, TextWriter output, TextWriter error, TimeProvider timeProvider, CancellationToken ct)
    {
        var manifest = await Create(root, version, baselineVersion, output, timeProvider, ct).ConfigureAwait(false);
        await Consume(root, manifest, manifest, aot, output, error, ct).ConfigureAwait(false);
    }

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
            using var stream = File.OpenRead(path);
            hashes[Path.GetFileName(path)] = Convert.ToHexStringLower((await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)));
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

    public static async Task Consume(
        string root, string manifest, string destination, bool aot, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var evidence = CandidateManifest.ReadAndVerify(manifest);
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";
        evidence[platform] = "failed";
        try
        {
            await PackageConsumptionCommand.Run(
                root, Path.GetDirectoryName(manifest)!, evidence["packageVersion"]!.GetValue<string>(), aot, output, error, ct).ConfigureAwait(false);
            evidence[platform] = "passed";
            if (aot)
            {
                evidence[OperatingSystem.IsWindows() ? "windowsNativeAot" : "linuxNativeAot"] = "passed";
            }
        }
        finally
        {
            CandidateManifest.Write(destination, evidence);
        }
    }
}
