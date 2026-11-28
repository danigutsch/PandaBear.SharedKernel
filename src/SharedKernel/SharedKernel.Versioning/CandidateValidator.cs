using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using SharedKernel.Processes;

namespace SharedKernel.Versioning;

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

    /// <summary>Compares the verified archives of two candidates and writes a separate API comparison report.</summary>
    /// <param name="baselineManifest">The baseline manifest path.</param>
    /// <param name="candidateManifest">The candidate manifest path.</param>
    /// <param name="apiCompatPath">The Microsoft ApiCompat executable path.</param>
    /// <param name="outputPath">A new report file path.</param>
    /// <param name="ct">Cancels the comparison and ends a running child process.</param>
    /// <returns>Whether the comparison completed with no compatibility differences.</returns>
    /// <exception cref="ArgumentException">Candidate evidence or a path is invalid.</exception>
    /// <exception cref="IOException">An input cannot be read or the report cannot be created.</exception>
    /// <exception cref="InvalidOperationException">The tool could not start or could not complete a comparison.</exception>
    public Task<bool> CompareApi(string baselineManifest, string candidateManifest, string apiCompatPath, string outputPath, CancellationToken ct) =>
        CandidateApiComparison.Run(_commands, baselineManifest, candidateManifest, apiCompatPath, outputPath, ct);

    /// <summary>Compares NuGet dependencies and bundled tool runtime graphs from exact candidate archives.</summary>
    /// <param name="baselineManifest">The baseline manifest path.</param>
    /// <param name="candidateManifest">The candidate manifest path.</param>
    /// <param name="outputPath">A new JSON report path.</param>
    /// <param name="ct">Cancels archive comparison and report writing.</param>
    /// <returns>Whether no dependency differences were found.</returns>
    /// <exception cref="ArgumentException">Candidate evidence, archive metadata, or a path is invalid.</exception>
    /// <exception cref="IOException">Input cannot be read or the report cannot be created.</exception>
    public static Task<bool> CompareDependencies(string baselineManifest, string candidateManifest, string outputPath, CancellationToken ct) =>
        CandidateDependencyComparison.Run(baselineManifest, candidateManifest, outputPath, ct);

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
        await Consume(
            new CandidateConsumptionOptions(options.RepoRoot, manifest, manifest, options.NativeAot) { Frameworks = options.Frameworks }, output, error, ct)
            .ConfigureAwait(false);
        return manifest;
    }

    /// <summary>Verifies the source provenance of selected packaged portable PDBs and writes separate evidence.</summary>
    /// <param name="candidateManifest">The candidate manifest that binds the package archive hashes.</param>
    /// <param name="symbolInventory">The SHA-256 inventory for the selected symbol archives beside the candidate.</param>
    /// <param name="sourcePattern">An HTTPS source URL pattern with a revision segment and trailing wildcard.</param>
    /// <param name="toolPackages">Tool package IDs whose own and selected bundled symbols should also be checked.</param>
    /// <param name="outputPath">A new report path.</param>
    /// <param name="ct">Cancels reading, verification, and the report write.</param>
    /// <returns>Whether every selected symbol matched its assembly and the expected source revision.</returns>
    /// <exception cref="ArgumentException">The package evidence, symbol inventory, or caller pattern is invalid.</exception>
    /// <exception cref="IOException">An input cannot be read or the report cannot be created.</exception>
    public static Task<bool> VerifySourceLink(
        string candidateManifest, string symbolInventory, string sourcePattern, IReadOnlyList<string> toolPackages,
        string outputPath, CancellationToken ct) =>
        CandidateSourceLinkVerification.Run(candidateManifest, symbolInventory, sourcePattern, toolPackages, outputPath, ct);

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
    /// then installs and runs the packages on the current platform for each framework and records <c>passed</c> or <c>failed</c> in the
    /// evidence at the destination, with the consumed frameworks under <c>&lt;platform&gt;PackageConsumptionFrameworks</c> and, with
    /// NativeAOT, <c>&lt;platform&gt;NativeAotFrameworks</c>. A canceled run records nothing.
    /// </summary>
    /// <param name="options">The manifest, destination, repository root, frameworks, and NativeAOT choice.</param>
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
        CandidateManifest.RequireListedPackagesOnly(feed, evidence["packageSha256"]!.AsObject());
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";
        var frameworks = options.Frameworks ?? RepositoryFrameworks.Read(root);
        var consumed = new JsonArray([.. frameworks.Select(static framework => JsonValue.Create(framework))]);
        try
        {
            await PackageConsumption.Run(
                _commands, new PackageConsumptionOptions(root, feed, version, aot) { Frameworks = frameworks }, output, error, ct)
                .ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A canceled run proves nothing, so it leaves the recorded evidence unchanged.
            evidence[platform] = "failed";
            evidence.Remove(platform + "Frameworks");
            new CandidateManifest(evidence).Save(destination);
            throw;
        }

        evidence[platform] = "passed";
        evidence[platform + "Frameworks"] = consumed;
        if (aot)
        {
            var nativeAot = OperatingSystem.IsWindows() ? "windowsNativeAot" : "linuxNativeAot";
            evidence[nativeAot] = "passed";
            evidence[nativeAot + "Frameworks"] = consumed.DeepClone();
        }

        new CandidateManifest(evidence).Save(destination);
    }
}
