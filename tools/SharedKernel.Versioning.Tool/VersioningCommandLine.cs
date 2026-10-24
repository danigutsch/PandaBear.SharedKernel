using System.CommandLine;
using System.Globalization;

namespace PandaBear.Versioning.Tool;

internal static class VersioningCommandLine
{
    private const int CommandFailed = 2;

    private const int NoBreakingChangeMarker = 3;

    private const string BaselineVersionDescription =
        "Published package version to compare APIs against. Requires PandaBear.SharedKernel.* mapped only to an existing local feed in NuGet.Config.";

    public static RootCommand Create(TextReader input, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new RootCommand("Calculates SharedKernel versions and validates, packs, and stages SharedKernel packages.")
        {
            CreateClassifyCommit(),
            CreateCompute(input),
            CreateCalculateRelease(),
            CreateCheckPublicApiBaselines(),
            CreateValidatePackageMetadata(),
            CreateHasBreakingChangeMarker(),
            CreateCheckApiCompatibility(timeProvider),
            CreatePackSharedKernel(timeProvider),
            CreatePrepareRelease(input),
            CreateCreateCandidate(timeProvider),
            CreateValidateCandidate(timeProvider),
            CreateConsumeCandidate(),
            CreateConsumePackages(),
            CreateStageCandidate(),

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
            await output.WriteLineAsync(ReleaseImpactText.ToOutputValue(commit.Impact)).ConfigureAwait(false);
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
            await output.WriteLineAsync(VersionOutputJson.Serialize(version)).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCalculateRelease()
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
        var command = new Command("calculate-release", "Calculates the version from Git history since the latest v* tag and prints version JSON.")
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
        SetAction(command, (parseResult, output, ct) => ReleaseVersionCommand.Run(
            new CalculateReleaseOptions(
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetRequiredValue(versionKind),
                parseResult.GetValue(runNumber),
                parseResult.GetValue(sha),
                parseResult.GetValue(gitHubOutput),
                parseResult.GetValue(gitHubSummary)),
            output,
            ct));
        return command;
    }

    private static Command CreateCheckPublicApiBaselines()
    {
        var repoRoot = RepoRootOption();
        var command = new Command("check-public-api-baselines", "Checks that every SharedKernel package project has PublicAPI baseline files.")
        {
            repoRoot,
        };
        SetAction(command, (parseResult, output, _) => PublicApiBaselineCommand.Run(parseResult.GetRequiredValue(repoRoot), output));
        return command;
    }

    private static Command CreateValidatePackageMetadata()
    {
        var repoRoot = RepoRootOption();
        var command = new Command("validate-package-metadata", "Validates the required package metadata of SharedKernel packages.")
        {
            repoRoot,
        };
        SetAction(command, (parseResult, output, _) =>
        {
            PackageMetadataValidationCommand.Run(parseResult.GetRequiredValue(repoRoot), output);
            return Task.CompletedTask;
        });
        return command;
    }

    private static Command CreateHasBreakingChangeMarker()
    {
        var range = new Argument<string>("git-range") { Description = "Git revision range, such as origin/main..HEAD." };

        // An empty range names no revision, and Git reads a leading '-' as an option: --output=<path> would write a file.
        range.Validators.Add(static result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
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
            if (await BreakingChangeMarkerCommand.Run(gitRange, parseResult.GetRequiredValue(repoRoot), output, ct).ConfigureAwait(false))
            {
                return 0;
            }

            await parseResult.InvocationConfiguration.Error.WriteLineAsync($"No breaking-change marker found in {gitRange}.").ConfigureAwait(false);
            return NoBreakingChangeMarker;
        });
        return command;
    }

    private static Command CreateCheckApiCompatibility(TimeProvider timeProvider)
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
        SetAction(command, (parseResult, output, ct) => ApiCompatibilityCommand.Run(
            new ApiCompatibilityOptions(
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

    private static Command CreatePackSharedKernel(TimeProvider timeProvider)
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
        var command = new Command("pack-sharedkernel", "Packs SharedKernel packages and verifies that they restore from the local feed.")
        {
            version,
            assemblyVersion,
            fileVersion,
            informationalVersion,
            outputRoot,
            skipRestoreCheck,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => SharedKernelPackCommand.Run(
            new PackSharedKernelOptions(
                parseResult.GetValue(version) ?? LocalAlphaVersion(timeProvider),
                parseResult.GetRequiredValue(outputRoot),
                VerifyRestore: !parseResult.GetValue(skipRestoreCheck),
                parseResult.GetRequiredValue(repoRoot),
                parseResult.GetValue(assemblyVersion),
                parseResult.GetValue(fileVersion),
                parseResult.GetValue(informationalVersion)),
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
            await output.WriteLineAsync("Release prep artifacts: " + options.OutputDirectory).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreateCreateCandidate(TimeProvider timeProvider)
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
        SetAction(command, (parseResult, output, ct) => CandidateCommand.Create(
            Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
            parseResult.GetValue(version)?.ToString(),
            parseResult.GetValue(baselineVersion)?.ToString(),
            output,
            timeProvider,
            ct));
        return command;
    }

    private static Command CreateValidateCandidate(TimeProvider timeProvider)
    {
        var version = SemanticVersionOption("--version", "Unique prerelease package version. Default: <base>-alpha.local.t<UTC timestamp>.g<revision>.");
        var baselineVersion = SemanticVersionOption("--baseline-version", BaselineVersionDescription);
        var aot = AotOption();
        var repoRoot = RepoRootOption();
        var command = new Command("validate-candidate", "Creates a candidate and consumes its packages on this platform.")
        {
            version,
            baselineVersion,
            aot,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => CandidateCommand.Run(
            Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
            parseResult.GetValue(version)?.ToString(),
            parseResult.GetValue(baselineVersion)?.ToString(),
            parseResult.GetValue(aot),
            output,
            parseResult.InvocationConfiguration.Error,
            timeProvider,
            ct));
        return command;
    }

    private static Command CreateConsumeCandidate()
    {
        var candidate = new Option<string>("--candidate") { Description = "Candidate manifest. Specify this or --candidate-directory.", HelpName = "path" };
        var candidateDirectory = new Option<string>("--candidate-directory")
        {
            Description = "Directory searched recursively for exactly one candidate.json. Specify this or --candidate.",
            HelpName = "path",
        };
        var evidence = new Option<string>("--output") { Description = "Evidence file to write. Default: the candidate manifest.", HelpName = "path" };
        var aot = AotOption();
        var repoRoot = RepoRootOption();
        var command = new Command("consume-candidate", "Verifies candidate package hashes, consumes the packages on this platform, and records the result.")
        {
            candidate,
            candidateDirectory,
            evidence,
            aot,
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
            return CandidateCommand.Consume(
                Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
                manifest,
                parseResult.GetValue(evidence) is { } destination ? Path.GetFullPath(destination) : manifest,
                parseResult.GetValue(aot),
                output,
                parseResult.InvocationConfiguration.Error,
                ct);
        });
        return command;
    }

    private static Command CreateConsumePackages()
    {
        var feed = new Option<string>("--feed") { Description = "Directory containing the packages.", HelpName = "path", Required = true };
        var version = SemanticVersionOption("--version", "Package version.");
        version.Required = true;
        var aot = AotOption();
        var repoRoot = RepoRootOption();
        var command = new Command("consume-packages", "Restores, builds, and runs the samples against the packages in a feed.")
        {
            feed,
            version,
            aot,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => PackageConsumptionCommand.Run(
            Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
            Path.GetFullPath(parseResult.GetRequiredValue(feed)),
            parseResult.GetRequiredValue(version).ToString(),
            parseResult.GetValue(aot),
            output,
            parseResult.InvocationConfiguration.Error,
            ct));
        return command;
    }

    private static Command CreateStageCandidate()
    {
        var candidate = new Option<string>("--candidate") { Description = "Pinned candidate manifest.", HelpName = "path", Required = true };
        var feed = new Option<string>("--feed") { Description = "Directory containing the candidate packages.", HelpName = "path", Required = true };
        var destination = new Option<string>("--output") { Description = "Local feed directory that receives the verified packages.", HelpName = "path", Required = true };
        var repoRoot = RepoRootOption();
        var command = new Command("stage-candidate", "Verifies a candidate against the checkout revision and copies its packages to a local feed.")
        {
            candidate,
            feed,
            destination,
            repoRoot,
        };
        SetAction(command, (parseResult, output, ct) => CandidateStagingCommand.Run(
            Path.GetFullPath(parseResult.GetRequiredValue(repoRoot)),
            Path.GetFullPath(parseResult.GetRequiredValue(candidate)),
            Path.GetFullPath(parseResult.GetRequiredValue(feed)),
            Path.GetFullPath(parseResult.GetRequiredValue(destination)),
            output,
            ct));
        return command;
    }

    private static Option<string> RepoRootOption() => new("--repo-root")
    {
        Description = "Repository root.",
        HelpName = "path",
        DefaultValueFactory = static _ => ".",
    };

    private static Option<bool> AotOption() => new("--aot") { Description = "Also publishes and runs the samples with NativeAOT." };

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
                await error.WriteLineAsync("Error: " + exception.Message).ConfigureAwait(false);
                return CommandFailed;
            }
        });
}
