using System.CommandLine;
using System.Globalization;
using SharedKernel.CI;
using SharedKernel.Processes;

namespace SharedKernel.Versioning.Tool;

/// <summary>Builds the command line of the versioning tool.</summary>
internal static class VersioningCommandLine
{
    private const int CommandFailed = 2;

    private const int NoBreakingChangeMarker = 3;
    private const int ApiDifferencesFound = 3;

    private const string BaselineVersionDescription =
        "Published package version to compare APIs against. Requires PandaBear.SharedKernel.* mapped only to an existing local feed in NuGet.Config.";

    /// <summary>
    /// Creates the root command with all versioning subcommands, reading piped input from <paramref name="input"/> and
    /// running processes as they are, with executables resolved from the absolute <c>PATH</c> directories.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(TextReader input, TimeProvider timeProvider) =>
        Create(input, timeProvider, new SystemProcessRunner(), PathExecutableResolver.Default);

    /// <summary>Creates the root command with all versioning subcommands, reading piped input from <paramref name="input"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static RootCommand Create(TextReader input, TimeProvider timeProvider, IProcessRunner processRunner, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(executableResolver);

        var packager = new Packager(processRunner, executableResolver);
        var candidates = new CandidateValidator(processRunner, executableResolver, timeProvider);
        var history = new ReleaseHistory(processRunner, executableResolver);
        return new RootCommand("Calculates SharedKernel versions, validates, packs, and consumes SharedKernel packages, and verifies consumers against candidates.")
        {
            CreateClassifyCommit(),
            CreateCompute(input),
            CreateCalculateRelease(history),
            CreateCheckPublicApiBaselines(),
            CreateValidatePackageMetadata(),
            CreateHasBreakingChangeMarker(history),
            CreateCheckApiCompatibility(packager, timeProvider),
            CreatePackSharedKernel(packager, timeProvider),
            CreatePackDev(packager),
            CreatePrepareRelease(input),
            CreateVerifyLockCoverage(),
            CreateCreateCandidate(candidates),
            CreateCompareCandidates(candidates),
            CreateValidateCandidate(candidates),
            CreateConsumeCandidate(candidates),
            CreateConsumePackages(packager),
            CreateMergeEvidence(),
            CreateVerifyConsumer(new ConsumerVerifier(processRunner, executableResolver)),

            // [diagram] is not a default directive. Consuming repositories' caller tests parse their command lines with it,
            // which prints the parse without running the command and exits 1 when the command line does not parse.
            new DiagramDirective(),
        };
    }

    private static Command CreateClassifyCommit()
    {
        var message = new Argument<string[]>("message")
        {
            Description = "Conventional Commit message. Separate arguments are joined with spaces.",
            Arity = ArgumentArity.OneOrMore,
        };
        var command = new Command("classify-commit", "Prints the release impact of a Conventional Commit message: major, minor, patch, or none.")
        {
            message,
        };
        SetAction(command, async (parseResult, output, _) =>
        {
            var commit = ConventionalCommitParser.Parse(string.Join(' ', parseResult.GetRequiredValue(message)));
            await output.WriteLineAsync(ReleaseImpactText.ToOutputValue(commit.Impact).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCompute(TextReader input)
    {
        var baseVersion = SemanticVersionOption("--base", "Base semantic version.");
        baseVersion.Required = true;
        var prerelease = new Option<string>("--prerelease") { Description = "Prerelease label, such as alpha.1.", HelpName = "label" };
        var sha = new Option<string>("--sha") { Description = "Source revision added to the informational version.", HelpName = "sha" };
        var command = new Command("compute", "Prints version JSON for commit messages read from standard input.")
        {
            baseVersion,
            prerelease,
            sha,
        };
        SetAction(command, async (parseResult, output, _) =>
        {
            var messages = await CommitMessageInput.ReadMessages(input).ConfigureAwait(false);
            var version = VersionCalculation.Calculate(
                parseResult.GetRequiredValue(baseVersion),
                messages,
                parseResult.GetValue(prerelease),
                parseResult.GetValue(sha));
            await output.WriteLineAsync(VersionOutputJson.Serialize(version).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCalculateRelease(ReleaseHistory history)
    {
        var versionKind = new Option<string>("--version-kind")
        {
            Description = "Version kind. prerelease prints <target>-alpha.<run-number> and requires --run-number.",
            DefaultValueFactory = static _ => "prerelease",
        };
        versionKind.AcceptOnlyFromAmong("prerelease", "stable");
        var runNumber = new Option<string>("--run-number") { Description = "CI run number for the prerelease label.", HelpName = "number" };
        var sha = new Option<string>("--sha") { Description = "Source revision. Default: HEAD.", HelpName = "sha" };
        var gitHubOutput = new Option<string>("--github-output") { Description = "GitHub Actions output file to append version values to.", HelpName = "path" };
        var gitHubSummary = new Option<string>("--github-summary") { Description = "GitHub Actions step summary file to append to.", HelpName = "path" };
        var repoRoot = RepoRootOption();
        var command = new Command("calculate", "Calculates the version from Git history since the latest v* tag and prints version JSON.")
        {
            versionKind,
            runNumber,
            sha,
            gitHubOutput,
            gitHubSummary,
            repoRoot,
        };
        command.Validators.Add(result =>
        {
            // Read the token, not the value: an unknown kind already has its own parse error.
            var kind = result.GetResult(versionKind)?.Tokens is [.., var token] ? token.Value : "prerelease";
            if (kind == "prerelease" && string.IsNullOrWhiteSpace(result.GetValue(runNumber)))
            {
                result.AddError($"Option '{runNumber.Name}' is required when '{versionKind.Name}' is prerelease.");
            }
        });
        SetAction(command, async (parseResult, output, ct) =>
        {
            var result = await history.CalculateVersion(
                new ReleaseVersionOptions(
                    parseResult.GetRequiredValue(repoRoot),
                    parseResult.GetRequiredValue(versionKind),
                    parseResult.GetValue(runNumber),
                    parseResult.GetValue(sha)),
                ct).ConfigureAwait(false);
            if (parseResult.GetValue(gitHubOutput) is { } outputFile && !string.IsNullOrWhiteSpace(outputFile))
            {
                await new KeyValueOutputFile(Path.GetFullPath(outputFile)).Write(ReleaseVersionCalculator.CreateOutputs(result), ct).ConfigureAwait(false);
            }

            if (parseResult.GetValue(gitHubSummary) is { } summaryFile && !string.IsNullOrWhiteSpace(summaryFile))
            {
                await new MarkdownSummaryFile(Path.GetFullPath(summaryFile)).Append(string.Join(Environment.NewLine, ReleaseVersionCalculator.CreateSummary(result)) + Environment.NewLine, ct).ConfigureAwait(false);
            }

            await output.WriteLineAsync(result.VersionJson.AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCheckPublicApiBaselines()
    {
        var repoRoot = RepoRootOption();
        var command = new Command("check-public-api-baselines", "Checks that every SharedKernel package project has PublicAPI baseline files.")
        {
            repoRoot,
        };
        SetAction(command, async (parseResult, output, _) =>
        {
            PublicApiBaselineChecker.EnsureBaselinesPresent(parseResult.GetRequiredValue(repoRoot));
            await output.WriteLineAsync("Public API baselines are present for SharedKernel projects.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateValidatePackageMetadata()
    {
        var repoRoot = RepoRootOption();
        var command = new Command("validate-package-metadata", "Validates the required package metadata of SharedKernel packages.")
        {
            repoRoot,
        };
        SetAction(command, async (parseResult, output, _) =>
        {
            PackageMetadataValidator.Validate(parseResult.GetRequiredValue(repoRoot));
            await output.WriteLineAsync("Package metadata validation passed.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateHasBreakingChangeMarker(ReleaseHistory history)
    {
        var range = new Argument<string>("git-range") { Description = "Git revision range, such as origin/main..HEAD." };

        // An empty range names no revision, and Git reads a leading '-' as an option: --output=<path> would write a file.
        range.Validators.Add(static result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-', StringComparison.Ordinal))
            {
                result.AddError("<git-range> requires a revision range that is not empty and does not start with '-'.");
            }
        });
        var repoRoot = RepoRootOption();
        var command = new Command("has-breaking-change-marker", "Succeeds when a commit in the range has a Conventional Commit breaking-change marker.")
        {
            range,
            repoRoot,
        };
        SetAction(command, async (parseResult, output, ct) =>
        {
            var gitRange = parseResult.GetRequiredValue(range);
            if (await history.HasBreakingChangeMarker(gitRange, parseResult.GetRequiredValue(repoRoot), ct).ConfigureAwait(false))
            {
                await output.WriteLineAsync("Breaking-change marker found.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
                return 0;
            }

            await parseResult.InvocationConfiguration.Error.WriteLineAsync($"No breaking-change marker found in {gitRange}.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return NoBreakingChangeMarker;
        });
        return command;
    }

    private static Command CreateCheckApiCompatibility(Packager packager, TimeProvider timeProvider)
    {
        var version = new Option<string>("--version") { Description = "Package version. Default: 0.1.0-alpha.local.<UTC timestamp>.", HelpName = "version" };
        var releasePhase = new Option<string>("--release-phase")
        {
            Description = "Failure policy: alpha only reports, beta only reports with --breaking-marker, rc and stable fail.",
            DefaultValueFactory = static _ => "alpha",
        };
        releasePhase.AcceptOnlyFromAmong("alpha", "beta", "rc", "stable");
        var baselineVersion = new Option<string>("--baseline-version") { Description = "Published package version to validate against.", HelpName = "version" };
        var breakingMarker = new Option<bool>("--breaking-marker") { Description = "The compared commits contain a breaking-change marker." };
        var outputRoot = new Option<string>("--output-root")
        {
            Description = "Directory for the report and packages.",
            HelpName = "path",
            DefaultValueFactory = static _ => "artifacts/api-compat",
        };
        var repoRoot = RepoRootOption();
        var command = new Command("check-api-compatibility", "Packs SharedKernel with package validation and writes the API compatibility report.")
        {
            version,
            releasePhase,
            baselineVersion,
            breakingMarker,
            outputRoot,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => packager.CheckApiCompatibility(
            new ApiCompatibilityReportOptions(
                parseResult.GetValue(version) ?? LocalAlphaVersion(timeProvider),
                parseResult.GetRequiredValue(outputRoot),
                parseResult.GetRequiredValue(releasePhase),
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetValue(baselineVersion),
                parseResult.GetValue(breakingMarker)),
            output,
            ct));
        return command;
    }

    private static Command CreatePackSharedKernel(Packager packager, TimeProvider timeProvider)
    {
        var version = new Option<string>("--version") { Description = "Package version. Default: 0.1.0-alpha.local.<UTC timestamp>.", HelpName = "version" };
        var assemblyVersion = new Option<string>("--assembly-version") { Description = "Assembly version.", HelpName = "version" };
        var fileVersion = new Option<string>("--file-version") { Description = "File version.", HelpName = "version" };
        var informationalVersion = new Option<string>("--informational-version") { Description = "Informational version.", HelpName = "version" };
        var outputRoot = new Option<string>("--output-root")
        {
            Description = "Directory that receives a folder named after the version.",
            HelpName = "path",
            DefaultValueFactory = static _ => "artifacts/packages/local",
        };
        var skipRestoreCheck = new Option<bool>("--skip-restore-check") { Description = "Skips restoring the packages from the local feed." };
        var repoRoot = RepoRootOption();
        var command = new Command("pack", "Packs SharedKernel packages and verifies that they restore from the local feed.")
        {
            version,
            assemblyVersion,
            fileVersion,
            informationalVersion,
            outputRoot,
            skipRestoreCheck,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => packager.Pack(
            new PackOptions(
                parseResult.GetValue(version) ?? LocalAlphaVersion(timeProvider),
                parseResult.GetRequiredValue(outputRoot),
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetValue(assemblyVersion),
                parseResult.GetValue(fileVersion),
                parseResult.GetValue(informationalVersion)),
            verifyRestore: !parseResult.GetValue(skipRestoreCheck),
            output,
            ct));
        return command;
    }

    private static Command CreatePackDev(Packager packager)
    {
        var projects = new Argument<string[]>("project")
        {
            Description = "Project to rebuild, such as SharedKernel.Results. Default: every SharedKernel project. Dependencies are packed only when the feed lacks them.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var feed = new Option<string>("--feed")
        {
            Description = "Feed directory that receives the packages; created when missing.",
            HelpName = "path",
            Required = true,
        };
        var version = new Option<string>("--version")
        {
            Description = "Package version. Default: " + DevPackOptions.DefaultVersion + ".",
            HelpName = "version",
            DefaultValueFactory = static _ => DevPackOptions.DefaultVersion,
        };
        var repoRoot = RepoRootOption();
        var command = new Command(
            "pack-dev",
            "Packs the named SharedKernel projects from the working tree into a feed and evicts them from the NuGet cache, so a consumer sees uncommitted changes in seconds.")
        {
            projects,
            feed,
            version,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => packager.PackDev(
            new DevPackOptions(
                parseResult.GetRequiredValue(feed),
                parseResult.GetRequiredValue(version),
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetValue(projects) ?? [],
                Directory.GetCurrentDirectory(),
                Environment.GetEnvironmentVariable("NUGET_PACKAGES")),
            output,
            ct));
        return command;
    }

    private static Command CreatePrepareRelease(TextReader input)
    {
        var version = new Option<string>("--version") { Description = "Release version.", HelpName = "version", Required = true };
        var packageDirectory = new Option<string>("--package-dir") { Description = "Directory containing the release packages.", HelpName = "path", Required = true };
        var outputDirectory = new Option<string>("--output-dir")
        {
            Description = "Directory for the release artifacts.",
            HelpName = "path",
            DefaultValueFactory = static _ => "artifacts/release-prep",
        };
        var sourceTag = new Option<string>("--source-tag") { Description = "Previous release tag.", HelpName = "tag" };
        var releaseImpact = new Option<string>("--release-impact") { Description = "Release impact shown in the release notes.", HelpName = "impact" };
        var sha = new Option<string>("--sha") { Description = "Source revision shown in the release notes.", HelpName = "sha" };
        var repoRoot = RepoRootOption();
        var command = new Command(
            "prepare-release",
            "Writes release notes, changelog, package manifest, third-party notices, and SBOM for changes read from standard input.")
        {
            version,
            packageDirectory,
            outputDirectory,
            sourceTag,
            releaseImpact,
            sha,
            repoRoot,
        };
        SetAction(command, async (parseResult, output, ct) =>
        {
            var options = new ReleasePreparationOptions(
                parseResult.GetRequiredValue(version),
                parseResult.GetRequiredValue(packageDirectory),
                parseResult.GetRequiredValue(outputDirectory),
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetValue(sourceTag),
                parseResult.GetValue(releaseImpact),
                parseResult.GetValue(sha));
            var changes = await input.ReadToEndAsync(ct).ConfigureAwait(false);
            await ReleasePreparationArtifacts.Write(options, changes).ConfigureAwait(false);
            await output.WriteLineAsync(("Release prep artifacts: " + options.OutputDirectory).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateVerifyLockCoverage()
    {
        var repoRoot = RepoRootOption();
        var command = new Command(
            "verify-lock-coverage",
            "Fails when a source project without a packages.lock.json resolves, in its restored obj/project.assets.json, a package version that no committed lock file resolves. Run it after restoring the solution.")
        {
            repoRoot,
        };
        SetAction(command, async (parseResult, output, _) =>
        {
            var checkedProjects = PackageLockInventory.VerifyLockCoverage(parseResult.GetRequiredValue(repoRoot));
            await output.WriteLineAsync(
                $"Lock coverage verified: {checkedProjects} lock-free project(s) resolve only packages that a committed lock file resolves.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCreateCandidate(CandidateValidator candidates)
    {
        var version = SemanticVersionOption("--version", "Unique prerelease package version. Default: <base>-alpha.local.t<UTC timestamp>.g<revision>.");
        var baselineVersion = SemanticVersionOption("--baseline-version", BaselineVersionDescription);
        var repoRoot = RepoRootOption();
        var command = new Command("create-candidate", "Packs and validates a clean committed checkout and writes candidate.json with the package hashes.")
        {
            version,
            baselineVersion,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => candidates.Create(
            new CandidateOptions(
                Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
                parseResult.GetValue(version)?.ToString(),
                parseResult.GetValue(baselineVersion)?.ToString()),
            output,
            ct));
        return command;
    }

    private static Command CreateValidateCandidate(CandidateValidator candidates)
    {
        var version = SemanticVersionOption("--version", "Unique prerelease package version. Default: <base>-alpha.local.t<UTC timestamp>.g<revision>.");
        var baselineVersion = SemanticVersionOption("--baseline-version", BaselineVersionDescription);
        var aot = AotOption();
        var framework = FrameworkOption();
        var repoRoot = RepoRootOption();
        var command = new Command("validate-candidate", "Creates a candidate and consumes its packages on this platform.")
        {
            version,
            baselineVersion,
            aot,
            framework,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => candidates.Validate(
            new CandidateOptions(
                Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
                parseResult.GetValue(version)?.ToString(),
                parseResult.GetValue(baselineVersion)?.ToString(),
                parseResult.GetValue(aot))
            {
                Frameworks = parseResult.GetValue(framework) is { Length: > 0 } frameworks ? frameworks : null,
            },
            output,
            parseResult.InvocationConfiguration.Error,
            ct));
        return command;
    }

    private static Command CreateCompareCandidates(CandidateValidator candidates)
    {
        var baseline = new Option<string>("--baseline") { Description = "Baseline candidate.json.", HelpName = "path", Required = true };
        var candidate = new Option<string>("--candidate") { Description = "Candidate.json to check against the baseline.", HelpName = "path", Required = true };
        var apiCompat = new Option<string>("--apicompat") { Description = "Microsoft ApiCompat executable path.", HelpName = "path", Required = true };
        var report = new Option<string>("--output") { Description = "New JSON comparison report path.", HelpName = "path", Required = true };
        var command = new Command("compare-candidates", "Compares two hash-verified package candidates and writes separate API comparison evidence.")
        {
            baseline, candidate, apiCompat, report,
        };
        SetAction(command, async (parseResult, output, ct) =>
        {
            var destination = parseResult.GetRequiredValue(report);
            var passed = await candidates.CompareApi(parseResult.GetRequiredValue(baseline), parseResult.GetRequiredValue(candidate),
                parseResult.GetRequiredValue(apiCompat), destination, ct).ConfigureAwait(false);
            var summary = passed ? "Candidate API comparison passed." : "Candidate API comparison found differences.";
            await output.WriteLineAsync($"{summary} Report: {destination}".AsMemory(), ct).ConfigureAwait(false);
            return passed ? 0 : ApiDifferencesFound;
        });
        return command;
    }

    private static Command CreateConsumeCandidate(CandidateValidator candidates)
    {
        var candidate = new Option<string>("--candidate") { Description = "Candidate manifest. Specify this or --candidate-directory.", HelpName = "path" };
        var candidateDirectory = new Option<string>("--candidate-directory")
        {
            Description = "Directory searched recursively for exactly one candidate.json. Specify this or --candidate.",
            HelpName = "path",
        };
        var evidence = new Option<string>("--output") { Description = "Evidence file to write. Default: the candidate manifest.", HelpName = "path" };
        var aot = AotOption();
        var framework = FrameworkOption();
        var repoRoot = RepoRootOption();
        var command = new Command("consume-candidate", "Verifies candidate package hashes, consumes the packages on this platform, and records the result.")
        {
            candidate,
            candidateDirectory,
            evidence,
            aot,
            framework,
            repoRoot,
        };
        command.Validators.Add(result =>
        {
            var hasCandidate = result.GetResult(candidate) is not null;
            if (hasCandidate == (result.GetResult(candidateDirectory) is not null))
            {
                result.AddError(hasCandidate
                    ? $"Options '{candidate.Name}' and '{candidateDirectory.Name}' cannot be combined."
                    : $"Option '{candidate.Name}' or '{candidateDirectory.Name}' is required.");
            }
        });
        SetAction(command, (parseResult, output, ct) =>
        {
            var manifest = parseResult.GetValue(candidate) is { } path
                ? Path.GetFullPath(path)
                : CandidateManifest.Find(Path.GetFullPath(parseResult.GetRequiredValue(candidateDirectory)));
            return candidates.Consume(
                new CandidateConsumptionOptions(
                    Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
                    manifest,
                    parseResult.GetValue(evidence) is { } destination ? Path.GetFullPath(destination) : manifest,
                    parseResult.GetValue(aot))
                {
                    Frameworks = parseResult.GetValue(framework) is { Length: > 0 } frameworks ? frameworks : null,
                },
                output,
                parseResult.InvocationConfiguration.Error,
                ct);
        });
        return command;
    }

    private static Command CreateMergeEvidence()
    {
        var candidate = new Option<string>("--candidate") { Description = "Candidate manifest to extend.", HelpName = "path", Required = true };
        var evidence = new Option<string[]>("--evidence")
        {
            Description = "Evidence files written by consume-candidate --output, one or more.",
            HelpName = "path",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };
        var destination = new Option<string>("--output") { Description = "Manifest to write. Default: the candidate manifest.", HelpName = "path" };
        var command = new Command("merge-evidence", "Merges passed per-platform consumption and NativeAOT results from evidence files into a candidate manifest.")
        {
            candidate,
            evidence,
            destination,
        };
        SetAction(command, (parseResult, output, ct) =>
        {
            var path = Path.GetFullPath(parseResult.GetRequiredValue(candidate));
            return CandidateValidator.MergeEvidence(
                path,
                [.. parseResult.GetRequiredValue(evidence).Select(Path.GetFullPath)],
                parseResult.GetValue(destination) is { } target ? Path.GetFullPath(target) : path,
                output,
                ct);
        });
        return command;
    }

    private static Command CreateConsumePackages(Packager packager)
    {
        var feed = new Option<string>("--feed") { Description = "Directory containing the packages.", HelpName = "path", Required = true };
        var version = SemanticVersionOption("--version", "Package version.");
        version.Required = true;
        var aot = AotOption();
        var framework = FrameworkOption();
        var repoRoot = RepoRootOption();
        var command = new Command(
            "consume-packages",
            "Installs the tool packages in a feed and runs their commands with --help, then restores, builds, and runs the samples against the packages.")
        {
            feed,
            version,
            aot,
            framework,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => packager.ConsumePackages(
            new PackageConsumptionOptions(
                Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
                Path.GetFullPath(parseResult.GetRequiredValue(feed)),
                parseResult.GetRequiredValue(version).ToString(),
                parseResult.GetValue(aot))
            {
                Frameworks = parseResult.GetValue(framework) is { Length: > 0 } frameworks ? frameworks : null,
            },
            output,
            parseResult.InvocationConfiguration.Error,
            ct));
        return command;
    }

    private static Command CreateVerifyConsumer(ConsumerVerifier verifier)
    {
        var candidate = new Option<string>("--candidate")
        {
            Description = "Candidate manifest, candidate.json, beside the packages it lists.",
            HelpName = "file",
            Required = true,
        };
        var solution = new Option<string>("--solution")
        {
            Description = "Solution whose projects' lock files are checked. Default: the only .slnx in the repository root.",
            HelpName = "file",
        };
        var evidence = new Option<string>("--output")
        {
            Description = "Evidence file to write, never the candidate manifest. Default: " + ConsumerVerifier.DefaultOutput + " in the repository root.",
            HelpName = "file",
        };
        var recipe = new Option<string>("--recipe")
        {
            Description = "Committed JSON command recipe inside the consumer repository.",
            HelpName = "file",
        };
        var recipeInput = new Option<string[]>("--recipe-input")
        {
            Description = "String value for a named recipe input. Repeat the option for each input.",
            HelpName = "name=value",
        };
        var repoRoot = RepoRootOption();
        var childCommand = new Argument<string[]>("command")
        {
            Description = "Command, after --, that restores, builds, and tests the consumer in the clone, such as dotnet test.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var command = new Command(
            "verify-consumer",
            "Runs a command on a clean clone of the consumer's committed revision with a candidate's packages and records the consumer compatibility evidence.")
        {
            candidate,
            solution,
            evidence,
            recipe,
            recipeInput,
            repoRoot,
            childCommand,
        };
        command.Validators.Add(result =>
        {
            var hasRecipe = result.GetResult(recipe) is not null;
            var hasCommand = result.GetValue(childCommand) is { Length: > 0 };
            if (!hasRecipe && result.GetResult(recipeInput) is not null)
            {
                result.AddError("Option '--recipe-input' requires '--recipe'.");
            }

            if (hasRecipe == hasCommand)
            {
                result.AddError(hasRecipe
                    ? "Option '--recipe' cannot be combined with a command."
                    : "Required argument 'command' missing for command: 'verify-consumer'. Specify a command or --recipe.");
            }
        });
        SetAction(command, (parseResult, output, ct) =>
        {
            var root = Path.GetFullPath(parseResult.GetRequiredValue(repoRoot));
            return verifier.Verify(
                new ConsumerVerificationOptions(
                    root,
                    Path.GetFullPath(parseResult.GetRequiredValue(candidate)),
                    parseResult.GetValue(solution) is { } path ? Path.GetFullPath(path) : null,
                    Path.GetFullPath(parseResult.GetValue(evidence) ?? Path.Combine(root, ConsumerVerifier.DefaultOutput)),
                    parseResult.GetValue(childCommand) ?? [])
                {
                    Recipe = parseResult.GetValue(recipe),
                    RecipeInputs = parseResult.GetValue(recipeInput) ?? [],
                },
                output,
                parseResult.InvocationConfiguration.Error,
                ct);
        });
        return command;
    }

    private static Option<string> RepoRootOption() => new("--repo-root")
    {
        Description = "Repository root.",
        HelpName = "path",
        DefaultValueFactory = static _ => ".",
    };

    private static Option<bool> AotOption() => new("--aot") { Description = "Also publishes and runs the samples with NativeAOT." };

    private static Option<string[]> FrameworkOption() => new("--framework")
    {
        Description = "Target framework to build the packed consumers for, such as net11.0. Repeat it to consume each framework in turn. Default: the framework of the repository root's Directory.Build.props.",
        HelpName = "tfm",
        CustomParser = result =>
        {
            var values = result.Tokens.Select(static token => token.Value).ToArray();
            foreach (var value in values)
            {
                if (!ConsumerFrameworks.IsValid(value))
                {
                    result.AddError($"Option '--framework' expects a target framework such as net10.0, but got '{value}'.");
                    return null;
                }
            }

            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            {
                result.AddError($"Option '--framework' lists '{values.GroupBy(static v => v, StringComparer.Ordinal).First(static g => g.Count() > 1).Key}' more than once.");
                return null;
            }

            return values;
        },
    };

    private static Option<SemanticVersion> SemanticVersionOption(string name, string description) => new(name)
    {
        Description = description,
        HelpName = "version",
        CustomParser = result =>
        {
            var token = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;
            try
            {
                return SemanticVersion.Parse(token);
            }
            catch (ArgumentException)
            {
                result.AddError($"Option '{name}' expects a semantic version, but got '{token}'.");
                return null;
            }
        },
    };

    private static string LocalAlphaVersion(TimeProvider timeProvider) =>
        "0.1.0-alpha.local." + timeProvider.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static void SetAction(Command command, Func<ParseResult, TextWriter, CancellationToken, Task> action) =>
        SetAction(command, async (parseResult, output, ct) =>
        {
            await action(parseResult, output, ct).ConfigureAwait(false);
            return 0;
        });

    // Maps domain failures to exit code 2; parse errors and cancellation keep System.CommandLine's codes.
    private static void SetAction(Command command, Func<ParseResult, TextWriter, CancellationToken, Task<int>> action) =>
        command.SetAction(async (parseResult, ct) =>
        {
            var error = parseResult.InvocationConfiguration.Error;
            try
            {
                return await action(parseResult, parseResult.InvocationConfiguration.Output, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (ct.IsCancellationRequested)
            {
                // Ctrl+C and SIGTERM sent to the process group also stop the running git, dotnet, or sample process, which
                // can fail before the token is observed. System.CommandLine exits 1 without output for a cancellation.
                throw new OperationCanceledException("The command was canceled.", exception, ct);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
            {
                await error.WriteLineAsync(("Error: " + exception.Message).AsMemory(), CancellationToken.None).ConfigureAwait(false);
                return CommandFailed;
            }
        });
}
