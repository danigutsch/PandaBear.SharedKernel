using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PandaBear.Processes;

namespace PandaBear.Versioning;

/// <summary>
/// Creates release candidates of the SharedKernel packages and records the evidence that they are consumable: packs a
/// clean, committed checkout with API validation into a feed with a <c>candidate.json</c> of package hashes, consumes the
/// listed packages on the current platform, and merges the per-platform results.
/// </summary>
public sealed class CandidateValidator
{
    private readonly ExternalCommands _commands;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the validator from the process runner it starts <c>git</c> and <c>dotnet</c> with, the resolver that finds them, and the clock of default versions.</summary>
    /// <param name="processRunner">Runs <c>git</c>, <c>dotnet</c>, and the packed tools and samples, such as <see cref="SystemProcessRunner"/>.</param>
    /// <param name="executableResolver">Finds <c>git</c> and <c>dotnet</c>, such as <see cref="PathExecutableResolver.Default"/>.</param>
    /// <param name="timeProvider">Supplies the UTC time of a default candidate version.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public CandidateValidator(IProcessRunner processRunner, IExecutableResolver executableResolver, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _commands = new ExternalCommands(processRunner, executableResolver);
        _timeProvider = timeProvider;
    }

    /// <summary>Creates the candidate, then consumes its packages on this platform and records the result in its manifest.</summary>
    /// <param name="options">The checkout, versions, and NativeAOT choice.</param>
    /// <param name="output">Receives the result lines.</param>
    /// <param name="error">Receives the output of the child processes.</param>
    /// <param name="ct">Cancels the run and ends the running process tree.</param>
    /// <returns>The path of the candidate manifest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The checkout is not clean, the baseline mapping is not exclusively a local feed, or a check failed.</exception>
    /// <exception cref="InvalidOperationException">A process could not start or failed.</exception>
    public async Task<string> Validate(CandidateOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var manifest = await Create(options, output, ct).ConfigureAwait(false);
        await Consume(new CandidateConsumptionOptions(options.RepoRoot, manifest, manifest, options.NativeAot), output, error, ct).ConfigureAwait(false);
        return manifest;
    }

    /// <summary>
    /// Requires a clean working tree, packs the candidate with API validation into
    /// <c>artifacts/api-compat/packages/&lt;version&gt;</c>, and writes its <c>candidate.json</c> with the package hashes.
    /// </summary>
    /// <param name="options">The checkout and versions; <see cref="CandidateOptions.NativeAot"/> is not used.</param>
    /// <param name="output">Receives the API compatibility report line.</param>
    /// <param name="ct">Cancels the run and ends the running process tree.</param>
    /// <returns>The path of the candidate manifest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The working tree is not clean, the baseline package mapping is not exclusively a local feed, or the pack failed.</exception>
    /// <exception cref="InvalidOperationException"><c>git</c> or <c>dotnet</c> could not start or failed.</exception>
    public async Task<string> Create(CandidateOptions options, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        var root = options.RepoRoot;
        var version = options.Version;
        var baselineVersion = options.BaselineVersion;
        if ((await _commands.Run("git", ["status", "--porcelain"], root, ct).ConfigureAwait(false)).Length != 0)
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

        var revision = (await _commands.Run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        var configured = XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("ComputedSemVer").First().Value;
        version ??= DefaultVersion(configured, _timeProvider.GetUtcNow(), revision);
        await ApiCompatibilityCheck.Run(
            _commands,
            new ApiCompatibilityReportOptions(version, Path.Combine(root, "artifacts/api-compat"), "beta", root, baselineVersion, BreakingMarker: false),
            output,
            ct).ConfigureAwait(false);
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
        new CandidateManifest(evidence).Save(manifest);
        return manifest;
    }

    /// <summary>
    /// Merges the per-platform results that <see cref="Consume"/> recorded in separate evidence files into the candidate
    /// manifest and writes it to <paramref name="destination"/>, so a consumer pin does not carry hand-edited results.
    /// Evidence travels without the packages, so the recorded identity and package hashes must match the candidate's.
    /// </summary>
    /// <param name="candidate">The candidate manifest to extend.</param>
    /// <param name="evidenceFiles">The evidence files, one or more.</param>
    /// <param name="destination">The manifest to write, which may be <paramref name="candidate"/> itself.</param>
    /// <param name="output">Receives one line per merged passed result.</param>
    /// <param name="ct">Cancels reading the files.</param>
    /// <returns>A task that completes when the manifest is written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An evidence file describes another candidate, or records a failed platform result. Nothing is written.
    /// </exception>
    public static Task MergeEvidence(string candidate, IReadOnlyList<string> evidenceFiles, string destination, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(evidenceFiles);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(output);
        return CandidateEvidenceMerge.Run(candidate, evidenceFiles, destination, output, ct);
    }

    /// <summary>
    /// Creates the default local candidate version from the configured version core, the UTC time and the library revision.
    /// </summary>
    internal static string DefaultVersion(string configuredSemVer, DateTimeOffset utcNow, string revision)
        => configuredSemVer.Split('-')[0] + "-alpha.local.t" + utcNow.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".g" + revision[..7];

    /// <summary>
    /// Verifies the manifest and its package hashes, requires the candidate directory to hold only the listed packages,
    /// then installs and runs the packages on the current platform and records <c>passed</c> or <c>failed</c> in the
    /// evidence at the destination. A canceled run records nothing.
    /// </summary>
    /// <param name="options">The manifest, destination, repository root, and NativeAOT choice.</param>
    /// <param name="output">Receives the result line.</param>
    /// <param name="error">Receives the output of the child processes.</param>
    /// <param name="ct">Cancels the run and ends the running process tree.</param>
    /// <returns>A task that completes when the evidence is written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The manifest, a package hash, or the candidate directory is invalid, or a check failed.</exception>
    /// <exception cref="InvalidOperationException">A process could not start or failed.</exception>
    public async Task Consume(CandidateConsumptionOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var (root, manifest, destination, aot) = options;
        var evidence = CandidateManifest.Load(manifest).Evidence;
        var version = evidence["packageVersion"]!.GetValue<string>();
        _ = SemanticVersion.Parse(version);
        var feed = Path.GetDirectoryName(manifest)!;
        RequireListedPackagesOnly(feed, evidence["packageSha256"]!.AsObject());
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";
        try
        {
            await PackageConsumption.Run(_commands, new PackageConsumptionOptions(root, feed, version, aot), output, error, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A canceled run proves nothing, so it leaves the recorded evidence unchanged.
            evidence[platform] = "failed";
            new CandidateManifest(evidence).Save(destination);
            throw;
        }

        evidence[platform] = "passed";
        if (aot)
        {
            evidence[OperatingSystem.IsWindows() ? "windowsNativeAot" : "linuxNativeAot"] = "passed";
        }

        new CandidateManifest(evidence).Save(destination);
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
