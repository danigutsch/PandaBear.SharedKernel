using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace PandaBear.Versioning.Tool;

internal static class CandidateCommand
{
    public static async Task Run(CandidateCommandOptions options, TextWriter output)
    {
        if (CommandRunner.Run("git", ["status", "--porcelain"], options.Root).Length != 0)
        {
            throw new ArgumentException("Commit library changes before creating candidate evidence.");
        }

        if (options.Baseline is not null)
        {
            var config = XDocument.Load(Path.Combine(options.Root, "NuGet.Config"));
            var mappings = config.Descendants("packageSource").Where(x => x.Elements("package").Any(p => (string?)p.Attribute("pattern") == "PandaBear.SharedKernel.*")).ToArray();
            if (mappings.Length != 1)
            {
                throw new ArgumentException("Baseline requires PandaBear.SharedKernel.* mapped exclusively to a local feed.");
            }

            var key = (string?)mappings[0].Attribute("key");
            var source = config.Descendants("packageSources").Elements("add").Single(x => (string?)x.Attribute("key") == key);
            var value = (string?)source.Attribute("value") ?? string.Empty;
            if (!Directory.Exists(Path.GetFullPath(value, options.Root)) || config.Descendants("packageSource").Any(x => x != mappings[0] && x.Elements("package").Any(p => ((string?)p.Attribute("pattern"))?.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal) == true)))
            {
                throw new ArgumentException("Internal baseline packages must map exclusively to an existing local feed.");
            }
        }

        var revision = CommandRunner.Run("git", ["rev-parse", "HEAD"], options.Root).Trim();
        var configured = XDocument.Load(Path.Combine(options.Root, "Directory.Build.props")).Descendants("ComputedSemVer").First().Value;
        var version = options.Version ?? configured.Split('-')[0] + "-alpha.local.t" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture) + ".g" + revision[..12];
        await ApiCompatibilityCommand.Run(new ApiCompatibilityOptions(version, Path.Combine(options.Root, "artifacts/api-compat"), "beta", options.Root, options.Baseline, false), output).ConfigureAwait(false);
        var feed = Path.Combine(options.Root, "artifacts/api-compat/packages", version);
        var hashes = new JsonObject();
        foreach (var path in Directory.GetFiles(feed, "*.nupkg").Order(StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(path);
            hashes[Path.GetFileName(path)] = Convert.ToHexStringLower((await SHA256.HashDataAsync(stream).ConfigureAwait(false)));
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
            ["apiBaselineComparison"] = options.Baseline is null ? "pending" : "passed",
            ["apiValidation"] = "passed",
            ["stablePromotion"] = "pending"
        };
        var manifest = Path.Combine(feed, "candidate.json");
        CandidateManifest.Write(manifest, evidence);
        await Consume(options with { Feed = feed, Version = version, Candidate = manifest }, output).ConfigureAwait(false);
    }

    public static async Task Consume(CandidateCommandOptions options, TextWriter output)
    {
        var manifest = options.Candidate ?? throw new ArgumentException("--candidate is required.");
        var evidence = CandidateManifest.ReadAndVerify(manifest);
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";
        var destination = options.Output ?? manifest;
        evidence[platform] = "failed";
        try
        {
            await PackageSmokeCommand.Run(options with { Feed = Path.GetDirectoryName(manifest), Version = evidence["packageVersion"]!.GetValue<string>() }, output).ConfigureAwait(false);
            evidence[platform] = "passed";
            if (options.Aot)
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
