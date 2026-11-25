using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using SharedKernel.CommandLine;
using SharedKernel.DevContainers;
using SharedKernel.Processes;
using SharedKernel.Repositories;
using SharedKernel.Repositories.GitHub;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>
/// Builds the <c>repo</c> command tree.
/// </summary>
internal static class RepoConfigCommandLine
{
    /// <summary>The opt-in command that needs time to clean up its private engine after a shutdown signal.</summary>
    internal const string IsolatedPodmanCommandName = "run-isolated-podman";

    private const int DefaultLimit = 10;

    private static readonly string[] GetQueries =
    [
        "next-priority",
        "next-unblocked",
        "next-work",
        "next-blockers",
        "next-enablers",
        "low-hanging-fruit",
        "pareto",
        "blocking-overview",
        "tags",
        "labels",
        "blockers-of",
        "by-tag",
        "by-label",
    ];

    private static readonly string[] VerbosityLevels =
        ["q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic"];

    /// <summary>
    /// Creates the root command. Actions write to the writers in the invocation configuration.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(RepoConfigToolEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var root = new RootOption(environment.WorkingDirectory);
        var command = new RootCommand("Maintains repository roadmap configuration, GitHub projection, and repository checks.")
        {
            CreateInitCommand(environment, root),
            CreateVerifyCommand(environment, root),
            CreateTrackedCheckCommand(root),
            CreateDiffCommand(environment, root),
            CreateSetCommand(environment, root),
            CreateGetCommand(environment, root),
            CreateGitHubCommand(environment, root),
            CreateAutomationCommand(environment, root),
            CreateVerifyTextEncodingCommand(environment, root),
            CreateVerifyJsonCommand(environment, root),
            CreateVerifyLineEndingsCommand(environment, root),
            CreateVerifyLinksCommand(environment, root),
            CreateVerifyGherkinCommand(environment, root),
            CreateVerifyActionPinsCommand(environment, root),
            CreateVerifyDependabotCommand(environment, root),
            CreateVerifyTestTaxonomyCommand(environment, root),
            CreateVerifyTargetFrameworksCommand(environment, root),
            CreateVerifyPodmanDnsCommand(environment),
            CreateRunIsolatedPodmanCommand(environment),
            CreateSecurityCommand(environment, root),
            CreateDependencyCommand(environment, root),
            CreateVerifyDevcontainerCommand(environment, root),
            CreateBaselineCommand(environment, root),
            CreateNewCommand(environment, root),
            CreateSeedDriftCommand(root),
        };

        // Consumer caller tests run "[diagram] <command line>" to parse a command line without running it.
        command.Directives.Add(new DiagramDirective());
        return command;
    }

    private static Command CreateInitCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("init", "Create the missing roadmap structure files. Existing files are kept.") { root };
        SetGuardedAction(command, (parseResult, output, _, _) =>
            Task.FromResult(RepoConfigToolApplication.Init(root.GetDirectory(parseResult), output)));
        return command;
    }

    private static Command CreateVerifyCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify", "Verify the roadmap structure, configuration, and items.") { root };
        SetGuardedAction(command, (parseResult, output, error, _) =>
            Task.FromResult(RepoConfigToolApplication.Verify(root.GetExistingDirectory(parseResult), output, error)));
        return command;
    }

    private static Command CreateTrackedCheckCommand(RootOption root)
    {
        Option<string> configuration = new("--config")
        {
            Description = "Path to the check configuration relative to the Git work tree root.",
            HelpName = "path",
            DefaultValueFactory = static _ => TrackedCheckRunner.ConfigurationPath,
        };
        Option<bool> strict = new("--strict") { Description = "Fail when a lane's tooling is unavailable." };
        Command command = new("check-tracked", "Run configured check lanes on nonempty tracked file selections.") { configuration, strict, root };
        SetGuardedAction(command, (parseResult, output, error, ct) => TrackedCheckRunner.Run(root.GetExistingDirectory(parseResult),
            parseResult.GetRequiredValue(configuration), parseResult.GetValue(strict), output, error, ct));
        return command;
    }

    private static Command CreateDiffCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("diff", "Report drift between the roadmap files and the expected structure.") { root };
        SetGuardedAction(command, (parseResult, output, error, _) =>
            Task.FromResult(RepoConfigToolApplication.Diff(root.GetExistingDirectory(parseResult), output, error)));
        return command;
    }

    private static Command CreateSetCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        string[] keys = [RoadmapConfigSetter.GitHubRepositoryKey];
        Argument<string> key = new("key")
        {
            Description = $"Configuration key: {string.Join(", ", keys)}.",
            HelpName = "key"
        };
        key.AcceptOnlyFromAmong(keys);
        Argument<string> value = new("value")
        {
            Description = "Value to store, such as owner/repository."
        };
        Command command = new("set", "Set a roadmap configuration value.") { key, value, root };
        SetGuardedAction(command, (parseResult, output, _, _) => Task.FromResult(RepoConfigToolApplication.Set(
            root.GetExistingDirectory(parseResult),
            parseResult.GetRequiredValue(key),
            parseResult.GetRequiredValue(value),
            output)));
        return command;
    }

    private static Command CreateGetCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Argument<string> query = new("query")
        {
            Description = $"One of: {string.Join(", ", GetQueries)}.",
            HelpName = "query"
        };
        query.AcceptOnlyFromAmong(GetQueries);
        Argument<string?> value = new("value")
        {
            Description = "The item ID for blockers-of, the tag for by-tag, or the label for by-label.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        Option<string> type = new("--type")
        {
            Description = "Only list items of this roadmap type."
        };
        Option<int> limit = new("--limit")
        {
            Description = "Maximum number of items to list.",
            DefaultValueFactory = _ => DefaultLimit,
            CustomParser = ParseLimit,
        };
        Command command = new("get", "List open roadmap items or roadmap metadata.") { query, value, type, limit, root };
        command.Validators.Add(result => ValidateQueryValue(result, query, value));
        SetGuardedAction(command, (parseResult, output, _, _) => Task.FromResult(RepoConfigToolApplication.Get(
            root.GetExistingDirectory(parseResult),
            parseResult.GetRequiredValue(query),
            parseResult.GetValue(value),
            parseResult.GetValue(type),
            parseResult.GetValue(limit),
            output)));
        return command;
    }

    private static Command CreateGitHubCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<bool> force = new("--force")
        {
            Description = "Write the changes instead of previewing them. Validation still applies."
        };

        Command sync = new("sync", "Project roadmap items to GitHub issues and Projects. Previews unless --force is given.") { force, root };
        SetGuardedAction(sync, (parseResult, output, _, ct) => RepoConfigToolApplication.SyncGitHub(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        Command reconcile = new("reconcile", "Update the reconciliation manifest from the open GitHub issues. Previews unless --force is given.") { force, root };
        SetGuardedAction(reconcile, (parseResult, output, _, ct) => RepoConfigToolApplication.ReconcileGitHub(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        Command import = new("import", "Import the reconciled GitHub issue snapshot into roadmap items. Previews unless --force is given.") { force, root };
        SetGuardedAction(import, (parseResult, output, _, ct) => RepoConfigToolApplication.ImportGitHub(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        return new Command("github", "Preview or write GitHub projection, reconciliation, import, issue links, and labels.")
        {
            sync, reconcile, import, CreateLinkSubissueCommand(environment), CreateSyncLabelsCommand(environment, root)
        };
    }

    private static Command CreateBaselineCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command verify = new("verify", "Report managed baseline files that are missing, outdated, edited, or unmanaged.") { root };
        SetGuardedAction(verify, (parseResult, output, error, ct) =>
            RepositoryBaseline.Verify(root.GetExistingDirectory(parseResult), output, error, ct));
        Option<bool> force = new("--force")
        {
            Description = "Write the files instead of previewing them. Never replaces an unmanaged or edited file unless its entry sets overwriteExisting."
        };
        Command apply = new("apply", "Create and update the managed baseline files from the consumer configuration. Previews unless --force is given.") { force, root };
        SetGuardedAction(apply, (parseResult, output, error, ct) => RepositoryBaseline.Apply(
            root.GetExistingDirectory(parseResult), parseResult.GetValue(force), output, error, ct));
        return new Command("baseline", "Verify or apply the repository baseline files: attributes, editor and ignore settings, README, templates, and wrappers.")
        {
            verify, apply
        };
    }

    private static Command CreateNewCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<string?> name = new("--name")
        {
            Description = "Repository name for the README. Defaults to the last segment of --root."
        };
        Option<string> branch = new("--branch")
        {
            Description = "Default branch name.",
            DefaultValueFactory = static _ => "main"
        };
        Option<string> language = new("--language")
        {
            Description = "Language that selects feature content: " + string.Join(", ", RepositoryLanguage.All.Select(static l => l.Id)) + ".",
            DefaultValueFactory = static _ => RepositoryLanguage.None.Id
        };
        Option<string?> sdkVersion = new("--sdk-version")
        {
            Description = "The .NET SDK version that global.json pins and Directory.Build.props targets, such as 10.0.100. Needed by the global-json and build-props features."
        };
        Option<string[]> feature = new("--feature")
        {
            Description = "Exact feature list; repeat for each feature: " + string.Join(", ", RepositoryFeature.All.Select(static f => f.Id)) + ". The defaults select standard language features. Signing and additional hooks are optional."
        };
        Option<string[]> with = new("--with")
        {
            Description = "Add a feature to the language defaults; repeat to add several. Cannot be combined with --feature.",
            HelpName = "feature"
        };
        Option<string?> licenseSource = new("--license-source")
        {
            Description = "Path to the caller's LICENSE template."
        };
        Option<string?> securitySource = new("--security-source")
        {
            Description = "Path to the caller's SECURITY.md template."
        };
        Option<string?> codeOwnersSource = new("--codeowners-source")
        {
            Description = "Path to the caller's CODEOWNERS template."
        };
        Option<string?> owner = new("--owner")
        {
            Description = "Value for @@owner@@ in a supplied policy template."
        };
        Option<string?> copyrightHolder = new("--copyright-holder")
        {
            Description = "Value for @@copyrightHolder@@ in a supplied policy template."
        };
        Option<string?> devcontainerImage = new("--devcontainer-image")
        {
            Description = "SHA-256 digest-pinned image for the opt-in devcontainer feature.",
        };
        Option<bool> force = new("--force")
        {
            Description = "Write the repository instead of previewing it. Never replaces an existing file or Git setting."
        };
        Command command = new("new", "Start a repository: write the chosen baseline files and their consumer configuration, and initialize Git. Previews unless --force is given.")
        {
            name, branch, language, sdkVersion, feature, with, force, root,
            licenseSource, securitySource, codeOwnersSource, owner, copyrightHolder, devcontainerImage
        };
        command.Validators.Add(result =>
        {
            if (result.GetResult(feature) is not null && result.GetResult(with) is not null)
            {
                result.AddError("--feature and --with cannot be combined.");
            }
        });
        SetGuardedAction(command, (parseResult, output, error, ct) =>
        {
            var languageId = parseResult.GetRequiredValue(language);
            var featureIds = parseResult.GetValue(feature) ?? [];
            if (parseResult.GetValue(with) is { Length: > 0 } additions &&
                RepositoryLanguage.TryParse(languageId, out var repositoryLanguage))
            {
                featureIds = [.. RepositoryFeature.DefaultsFor(repositoryLanguage).Select(static item => item.Id), .. additions];
            }

            return RepositoryScaffold.Run(
                root.GetDirectory(parseResult),
                parseResult.GetValue(name),
                parseResult.GetRequiredValue(branch),
                languageId,
                parseResult.GetValue(sdkVersion),
                featureIds,
                new RepositoryPolicySources(
                    ResolvePolicySource(parseResult.GetValue(licenseSource), environment.WorkingDirectory),
                    ResolvePolicySource(parseResult.GetValue(securitySource), environment.WorkingDirectory),
                    ResolvePolicySource(parseResult.GetValue(codeOwnersSource), environment.WorkingDirectory),
                    parseResult.GetValue(owner),
                    parseResult.GetValue(copyrightHolder)),
                parseResult.GetValue(devcontainerImage),
                parseResult.GetValue(force),
                output,
                error,
                ct);
        });
        return command;
    }

    private static string? ResolvePolicySource(string? path, string workingDirectory) =>
        string.IsNullOrWhiteSpace(path) ? path : Path.GetFullPath(path, Path.GetFullPath(workingDirectory));

    private static Command CreateSeedDriftCommand(RootOption root)
    {
        Option<string?> name = new("--name")
        {
            Description = "Repository name for AGENTS.md. Defaults to the last segment of the repository path."
        };
        Option<string?> sdkVersion = new("--sdk-version")
        {
            Description = "Expected .NET SDK version for the global-json and build-props features."
        };
        Option<string[]> feature = new("--feature")
        {
            Description = "Write-once feature to compare: agents, global-json, nuget-config, or build-props. Repeat to compare several."
        };
        Command command = new("seed-drift", "Compare selected write-once files to the current catalog without changing them.")
        {
            name, sdkVersion, feature, root
        };
        SetGuardedAction(command, (parseResult, output, _, ct) => RepositorySeedDrift.Run(
            root.GetDirectory(parseResult),
            parseResult.GetValue(name),
            parseResult.GetValue(sdkVersion),
            parseResult.GetValue(feature) ?? [],
            output,
            ct));
        return command;
    }

    private static Command CreateSyncLabelsCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        RepositoryOption repository = new("GitHub repository whose labels are synchronized, as owner/name; with --forgejo, a repository of that Forgejo instance.")
        {
            Required = true,
        };
        Option<bool> force = new("--force")
        {
            Description = "Create and update the labels instead of previewing them. Never deletes a label."
        };
        var forgejo = new ForgejoOption("Forgejo instance, such as https://codeberg.org, that hosts --repository instead of GitHub. Its token comes from FORGEJO_TOKEN.");
        Command command = new("sync-labels", "Create missing and update changed GitHub labels from the consumer configuration. Previews unless --force is given.")
        {
            repository, force, forgejo, root
        };
        SetGuardedAction(command, (parseResult, output, _, ct) => parseResult.GetValue(forgejo) is { } server
            ? RepoConfigToolApplication.SyncForgejoLabels(
                root.GetExistingDirectory(parseResult),
                parseResult.GetRequiredValue(repository),
                server,
                parseResult.GetValue(force),
                environment.ForgejoHandler,
                output,
                ct)
            : RepoConfigToolApplication.SyncLabels(
                root.GetExistingDirectory(parseResult),
                parseResult.GetRequiredValue(repository),
                parseResult.GetValue(force),
                environment.HttpClient,
                output,
                ct));
        return command;
    }

    private static Command CreateLinkSubissueCommand(RepoConfigToolEnvironment environment)
    {
        RepositoryOption repository = new("GitHub repository containing both issues, as owner/name.")
        {
            Required = true,
        };
        Option<int> parent = new("--parent")
        {
            Required = true,
            Description = "Positive parent issue number."
        };
        Option<int> child = new("--child")
        {
            Required = true,
            Description = "Positive child issue number."
        };
        Option<bool> force = new("--force")
        {
            Description = "Create and verify the link instead of previewing it. Never replaces a parent."
        };
        Command command = new("link-subissue", "Link an existing issue to a parent in the same repository. Previews unless --force is given.")
        {
            repository, parent, child, force
        };
        SetGuardedAction(command, (parseResult, output, _, ct) => RepoConfigToolApplication.LinkSubissue(
            parseResult.GetRequiredValue(repository), parseResult.GetRequiredValue(parent), parseResult.GetRequiredValue(child),
            parseResult.GetValue(force), environment.HttpClient, output, ct));
        return command;
    }

    private static Command CreateAutomationCommand(RepoConfigToolEnvironment environment, RootOption root) =>
        new("automation", "Run the integrity-bound roadmap maintenance steps in GitHub Actions.")
        {
            CreateAutomationStep(environment, root, "plan-github", "Create the roadmap maintenance plan.", RepoConfigToolApplication.PlanAutomation),
            CreateAutomationStep(environment, root, "validate-plan", "Revalidate the reviewed plan against live inputs.", RepoConfigToolApplication.ValidateAutomationPlan),
            CreateAutomationStep(environment, root, "apply-plan", "Apply the closure approvals in the reviewed plan.", RepoConfigToolApplication.ApplyAutomationPlan),
            CreateAutomationStep(environment, root, "seal-result", "Seal the changed roadmap paths.", RepoConfigToolApplication.SealAutomationResult),
            CreateAutomationStep(environment, root, "verify-result", "Verify the sealed result.", RepoConfigToolApplication.VerifyAutomationResult),
            CreatePublishResultCommand(environment, root),
        };

    private static Command CreateAutomationStep(
        RepoConfigToolEnvironment environment,
        RootOption root,
        string name,
        string description,
        Func<RoadmapAutomationService, TextWriter, CancellationToken, Task<int>> step)
    {
        Command command = new(name, description) { root };
        SetGuardedAction(command, async (parseResult, output, _, ct) =>
        {
            using var connection = GitHubConnections.FromGitHubToken(environment.HttpClient, "roadmap automation");
            return await step(CreateAutomationService(parseResult, root, environment, connection), output, ct).ConfigureAwait(false);
        });
        return command;
    }

    private static Command CreatePublishResultCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<bool> force = new("--force")
        {
            Description = "Publish the pull request instead of previewing it. Validation still applies."
        };
        Command command = new("publish-result", "Publish the sealed result as a draft pull request. Previews unless --force is given.") { force, root };
        SetGuardedAction(command, async (parseResult, output, _, ct) =>
        {
            using var connection = GitHubConnections.FromGitHubToken(environment.HttpClient, "roadmap automation");
            return await RepoConfigToolApplication.PublishAutomationResult(
                CreateAutomationService(parseResult, root, environment, connection),
                parseResult.GetValue(force),
                output,
                ct).ConfigureAwait(false);
        });
        return command;
    }

    private static RoadmapAutomationService CreateAutomationService(
        ParseResult parseResult,
        RootOption root,
        RepoConfigToolEnvironment environment,
        GitHubConnection connection) =>
        new(root.GetExistingDirectory(parseResult), connection, environment.GitRepository);

    private static Command CreateVerifyTextEncodingCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify-text-encoding", "Verify that text files in the Git index are valid UTF-8 without NUL bytes.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyTextEncoding(root.GetExistingDirectory(parseResult), output, error, ct));
        return command;
    }

    private static Command CreateVerifyGherkinCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Argument<string[]> paths = new("path")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Tracked feature files or directories to check instead of the configured include patterns. Relative to the root.",
        };
        Command command = new("verify-gherkin", "Verify tracked Gherkin feature files against the gherkin rules in .config/sharedkernel-repo.json.") { paths, root };
        SetGuardedAction(command, (parseResult, output, error, ct) => RepoConfigToolApplication.VerifyGherkin(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(paths) ?? [],
            output,
            error,
            ct));
        return command;
    }

    private static Command CreateVerifyJsonCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify-json", "Verify that the working-tree content of every JSON file in the Git index is UTF-8 JSON. Comments and trailing commas are allowed.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyJson(root.GetExistingDirectory(parseResult), output, error, ct));
        return command;
    }

    private static Command CreateVerifyLineEndingsCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify-line-endings", "Verify that files in the Git index and the working tree use the line endings that their eol attribute requires.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyLineEndings(root.GetExistingDirectory(parseResult), output, error, ct));
        return command;
    }

    private static Command CreateVerifyActionPinsCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<string> manifest = new("--manifest")
        {
            Description = $"Action pin manifest. Relative to the root; defaults to {ActionPinVerifier.DefaultManifestPath}.",
            HelpName = "path",
        };
        Command command = new("verify-action-pins", "Verify that every external uses: reference pins the full commit SHA and version comment that the action pin manifest approves.") { manifest, root };
        SetGuardedAction(command, (parseResult, output, error, ct) => RepoConfigToolApplication.VerifyActionPins(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(manifest),
            output,
            error,
            ct));
        return command;
    }

    private static Command CreateVerifyDependabotCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify-dependabot", "Verify that every Dependabot ignore entry names update-types, so ignored dependencies still receive security updates.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyDependabot(root.GetExistingDirectory(parseResult), output, error, ct));
        return command;
    }

    private static Command CreateVerifyTestTaxonomyCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<bool> report = new("--report") { Description = "Report taxonomy findings without failing the command." };
        Command command = new("verify-test-taxonomy", "Verify the declared SUTs, test types, names, and seam registers of tracked projects.") { report, root };
        SetGuardedAction(command, (parseResult, output, error, ct) => RepoConfigToolApplication.VerifyTestTaxonomy(
            root.GetExistingDirectory(parseResult), parseResult.GetValue(report), output, error, ct));
        return command;
    }

    private static Command CreateVerifyTargetFrameworksCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command command = new("verify-target-frameworks", "Verify that the tracked project files build the target frameworks that the targetFrameworks policy in .config/sharedkernel-repo.json expects.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyTargetFrameworks(root.GetExistingDirectory(parseResult), output, error, ct));
        return command;
    }

    private static Command CreateVerifyLinksCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Argument<string[]> paths = new("path")
        {
            Description = "Markdown or text files, or directories of Markdown files, to scan instead of the configured include patterns. Relative to the root.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        Command command = new("verify-links", "Verify local links, Markdown anchors, and issue references in documentation files.") { paths, root };
        SetGuardedAction(command, (parseResult, output, error, ct) => RepoConfigToolApplication.VerifyLinks(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(paths) ?? [],
            output,
            error,
            ct));
        return command;
    }

    private static Command CreateVerifyPodmanDnsCommand(RepoConfigToolEnvironment environment)
    {
        Option<string> verbosity = new("--verbosity")
        {
            Description = "Output detail: q[uiet], m[inimal], n[ormal], d[etailed], or diag[nostic]. Quiet prints nothing when the state is healthy.",
            HelpName = "level",
            DefaultValueFactory = _ => "normal",
        };
        verbosity.AcceptOnlyFromAmong(VerbosityLevels);
        Command command = new("verify-podman-dns", "Check rootless Podman aardvark-dns state without changing it.") { verbosity };
        SetGuardedAction(command, (parseResult, output, error, ct) => PodmanDnsPreflight.Run(
            environment.PodmanHost ?? PodmanDnsHost.Current(),
            parseResult.GetRequiredValue(verbosity) is "q" or "quiet",
            output,
            error,
            ct));
        return command;
    }

    private static Command CreateRunIsolatedPodmanCommand(RepoConfigToolEnvironment environment)
    {
        const int maxTimeoutSeconds = 4_294_967;
        Option<string> imageStore = new("--image-store")
        {
            Description = "Absolute path to an existing read-only additional overlay image store.",
            HelpName = "directory",
            Required = true,
        };
        Option<int> timeout = new("--timeout-seconds")
        {
            Description = "Maximum duration of the test command, in seconds.",
            DefaultValueFactory = _ => 1_800,
        };
        timeout.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is <= 0 or > maxTimeoutSeconds)
            {
                result.AddError($"--timeout-seconds must be between 1 and {maxTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}.");
            }
        });
        Option<string> networkHelper = new("--network-helper")
        {
            Description = "Installed rootless network helper. Defaults to pasta.",
            DefaultValueFactory = _ => "pasta",
        };
        networkHelper.AcceptOnlyFromAmong("pasta", "slirp4netns");
        Option<string?> runtimeParent = new("--runtime-parent")
        {
            Description = "Existing absolute directory for private transient engine state. The graph and volumes stay under TMPDIR.",
            HelpName = "directory",
        };
        Argument<string[]> child = new("command")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = "Trusted test executable and arguments. Put '--' before the executable to forward its options.",
        };
        Command command = new(IsolatedPodmanCommandName, "Run a test command with its own local rootless Podman engine.")
        {
            imageStore, timeout, networkHelper, runtimeParent, child
        };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
        {
            var arguments = parseResult.GetRequiredValue(child);
            var executable = PathExecutableResolver.Default.Resolve(arguments[0]);
            var startInfo = new ProcessStartInfo(executable) { WorkingDirectory = environment.WorkingDirectory };
            foreach (var argument in arguments.Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }
            var runner = environment.IsolatedPodmanCommandRunner
                ?? new BoundedProcessRunner(TimeSpan.FromSeconds(parseResult.GetRequiredValue(timeout)));
            return IsolatedPodman.Run(
                environment.PodmanHost ?? PodmanDnsHost.Current(),
                parseResult.GetRequiredValue(imageStore),
                parseResult.GetRequiredValue(networkHelper),
                parseResult.GetValue(runtimeParent),
                startInfo,
                runner,
                output,
                error,
                ct);
        });
        return command;
    }

    private static Command CreateDependencyCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<string> sha = new("--sha")
        {
            Required = true,
            Description = "Exact reviewed commit SHA."
        };
        Option<string> reference = new("--ref")
        {
            Required = true,
            Description = "Reviewed Git reference."
        };
        Option<string> job = new("--job")
        {
            Required = true,
            Description = "External workflow run ID."
        };
        Option<string> correlator = new("--correlator")
        {
            Required = true,
            Description = "Stable dependency snapshot stream identifier."
        };
        Option<string> destination = new("--output")
        {
            Required = true,
            Description = "New snapshot JSON file within the reviewed repository."
        };
        Command snapshot = new("snapshot", "Generate a complete committed NuGet lockfile snapshot without restoring packages.")
        {
            root, sha, reference, job, correlator, destination
        };
        SetGuardedAction(snapshot, (parseResult, output, error, cancellationToken) => RepoConfigToolApplication.WriteDependencySnapshot(
            new DependencySnapshotOptions(
                root.GetExistingDirectory(parseResult),
                parseResult.GetRequiredValue(sha),
                parseResult.GetRequiredValue(reference),
                parseResult.GetRequiredValue(job),
                parseResult.GetRequiredValue(correlator),
                parseResult.GetRequiredValue(destination)),
            output,
            error,
            cancellationToken));
        return new Command("dependency", "Generate, submit, and verify repository dependency metadata.")
        {
            snapshot,
            CreateRestoreGraphComparisonCommand(environment, root),
            CreateNuGetUpgradeTrialCommand(environment, root),
            CreatePinInspectionCommand(environment, root),
            CreateDependencySubmitCommand(environment, root),
            CreateVerifyLicenseExceptionsCommand(environment, root),
        };
    }

    private static Command CreatePinInspectionCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<string> package = new("--package")
        {
            Required = true,
            Description = "NuGet package ID to inspect."
        };
        Command command = new("inspect-pin", "Report the requested and resolved paths for a selected NuGet package.")
        {
            root, package,
        };
        SetGuardedAction(command, (parseResult, output, error, cancellationToken) => NuGetPinInspection.Run(
            root.GetExistingDirectory(parseResult), parseResult.GetRequiredValue(package), output, error, cancellationToken));
        return command;
    }

    private static Command CreateRestoreGraphComparisonCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        var candidate = CreatePathOption("--candidate", "Candidate Git checkout. Relative to the baseline root.");
        candidate.Required = true;
        var destination = CreatePathOption("--output", "New comparison JSON file within the baseline checkout.");
        destination.Required = true;
        var baselineVerification = new Option<string?>("--baseline-verification") { Description = "Optional CI manifest in the baseline checkout." };
        var candidateVerification = new Option<string?>("--candidate-verification") { Description = "Optional CI manifest in the candidate checkout." };
        Command command = new("compare-restore", "Compare the selected package graphs of two restored checkouts.")
        {
            root, candidate, destination, baselineVerification, candidateVerification,
        };
        SetGuardedAction(command, (parseResult, output, error, cancellationToken) => NuGetRestoreGraphComparison.Run(
            new NuGetRestoreGraphComparisonOptions(
                root.GetExistingDirectory(parseResult),
                parseResult.GetRequiredValue(candidate),
                parseResult.GetRequiredValue(destination),
                parseResult.GetValue(baselineVerification),
                parseResult.GetValue(candidateVerification)),
            output,
            error,
            cancellationToken));
        return command;
    }

    private static Command CreateNuGetUpgradeTrialCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        var plan = CreatePathOption("--plan", "New patch path, with a JSON receipt at that path plus .json. Relative to the root.");
        plan.Required = true;
        Option<string> props = new("--props")
        {
            Description = "Central .props file owning the selected declarations. Relative to the root.",
            DefaultValueFactory = _ => "Directory.Packages.props",
        };
        Option<string?> remove = new("--remove-pin") { Description = "Package ID of the transitive declaration to remove." };
        Option<string[]> updates = new("--update") { Description = "Exact central update in package@version form. Repeat for a family." };
        Option<bool> propose = new("--propose") { Description = "Ask the SDK to update direct owners of the selected pin." };
        Option<string[]> coordinate = new("--coordinate") { Description = "Include an additional direct package in the pin or audit proposal. Repeat for a family." };
        Option<string[]> sources = new("--copy-local-source") { Description = "Relative local source whose package archives are needed in the trial. Repeat as needed." };
        Option<bool> vulnerable = new("--vulnerable") { Description = "Find a minimum verified upgrade for known advisories." };
        Option<bool> build = new("--build") { Description = "Validate the candidate with the Release solution build." };
        Option<bool> test = new("--test") { Description = "Build and test the candidate solution in Release." };
        Command command = new("trial-upgrade", "Trial a NuGet upgrade and produce a reviewable props and lockfile patch.")
        {
            root, plan, props, remove, updates, propose, coordinate, sources, vulnerable, build, test,
        };
        SetGuardedAction(command, (parseResult, output, error, ct) => NuGetUpgradeTrial.Run(
            new NuGetUpgradeTrialOptions(
                root.GetExistingDirectory(parseResult), parseResult.GetRequiredValue(plan), parseResult.GetRequiredValue(props),
                parseResult.GetValue(remove), parseResult.GetValue(updates) ?? [], parseResult.GetValue(vulnerable),
                parseResult.GetValue(sources) ?? [], parseResult.GetValue(build), parseResult.GetValue(test))
            {
                ProposePinUpdates = parseResult.GetValue(propose),
                CoordinatedPackages = parseResult.GetValue(coordinate) ?? [],
            },
            output, error, ct));
        return command;
    }

    private static Command CreateDependencySubmitCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        var snapshot = CreatePathOption("--snapshot", "Dependency snapshot JSON file. Relative to the root.");
        snapshot.Required = true;
        RepositoryOption repository = new("GitHub repository that receives the snapshot.")
        {
            Required = true,
        };
        Option<bool> force = new("--force")
        {
            Description = "Submit the snapshot instead of previewing it. Validation still applies."
        };
        Command command = new("submit", "Submit a dependency snapshot to the GitHub dependency graph. Previews unless --force is given.")
        {
            snapshot, repository, force, root
        };
        SetGuardedAction(command, (parseResult, output, _, ct) => RepoConfigToolApplication.SubmitDependencySnapshot(
            new DependencySubmitOptions(
                root.GetExistingDirectory(parseResult),
                parseResult.GetRequiredValue(snapshot),
                parseResult.GetRequiredValue(repository),
                parseResult.GetValue(force)),
            environment.HttpClient,
            output,
            ct));
        return command;
    }

    private static Command CreateVerifyLicenseExceptionsCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        var githubOutput = CreatePathOption("--github-output", "GitHub Actions output file that receives packages, the approved package URLs. Relative to the root.");
        Command command = new("verify-license-exceptions", "Verify that the configured license exceptions match their approval and the locked package versions.")
        {
            githubOutput, root
        };
        SetGuardedAction(command, (parseResult, output, error, ct) => RepoConfigToolApplication.VerifyLicenseExceptions(
            root.GetExistingDirectory(parseResult),
            parseResult.GetValue(githubOutput),
            output,
            error,
            ct));
        return command;
    }

    private static Option<string> CreatePathOption(string name, string description) =>
        new(name)
        {
            Description = description,
            HelpName = "path",
            CustomParser = result =>
            {
                var path = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
                if (string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal))
                {
                    result.AddError($"{name} requires a file path.");
                    return null;
                }

                return path;
            },
        };

    private static Command CreateSecurityCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Command baseline = new("baseline", "Verify repository security document contracts.") { root };
        SetGuardedAction(baseline, (parseResult, output, error, ct) => RepositorySecurity.Run(
            root.GetExistingDirectory(parseResult), null, output, error, ct));
        Option<string> destination = new("--output")
        {
            Description = "Repository-relative or absolute report directory. Existing reports are preserved.",
            HelpName = "directory",
            Required = true,
            CustomParser = result =>
            {
                var path = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
                if (string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal))
                {
                    result.AddError("--output requires a report directory path.");
                    return null;
                }

                return path;
            },
        };
        Command scan = new("scan", "Verify the baseline and scan current source with the pinned Gitleaks executable.") { root, destination };
        SetGuardedAction(scan, (parseResult, output, error, ct) => RepositorySecurity.Run(
            root.GetExistingDirectory(parseResult), parseResult.GetRequiredValue(destination), output, error, ct));
        return new Command("security", "Verify security document contracts and scan current source.") { baseline, scan };
    }

    private static Command CreateVerifyDevcontainerCommand(RepoConfigToolEnvironment environment, RootOption root)
    {
        Option<string[]> commands = new("--exec")
        {
            Description = "Command to run with bash -lc inside the started container. Repeat to run several in order.",
            HelpName = "command",
        };
        commands.Validators.Add(result =>
        {
            if (result.Tokens.Any(static token => string.IsNullOrWhiteSpace(token.Value) || token.Value.Contains('\0', StringComparison.Ordinal)))
            {
                result.AddError("--exec requires a command.");
            }
        });
        Option<string> logDirectory = new("--log-directory")
        {
            Description = $"Directory that receives the logs and metadata.txt. Relative to the root. Defaults to {DevcontainerVerification.DefaultLogDirectory}.",
            HelpName = "directory",
            CustomParser = result =>
            {
                var path = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
                if (string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal))
                {
                    result.AddError("--log-directory requires a directory path.");
                    return null;
                }

                return path;
            },
        };
        Option<bool> keepContainer = new("--keep-container")
        {
            Description = "Keep the started container instead of removing it."
        };
        var cli = CreatePathOption(
            "--devcontainer-cli",
            $"Dev Container CLI to run. Relative to the root. Without it, the pinned CLI is bootstrapped under {DevcontainerVerification.ToolsDirectory} on Linux x64.");
        Command command = new("verify-devcontainer", "Start the repository dev container, run commands inside it, and remove it.")
        {
            commands, logDirectory, keepContainer, cli, root
        };
        SetGuardedAction(command, (parseResult, output, _, ct) =>
        {
            var rootPath = root.GetExistingDirectory(parseResult);
            var cliPath = parseResult.GetValue(cli);
            return DevcontainerVerification.Run(
                new DevcontainerVerificationOptions(
                    rootPath,
                    parseResult.GetValue(commands) ?? [],
                    Path.GetFullPath(parseResult.GetValue(logDirectory) ?? DevcontainerVerification.DefaultLogDirectory, rootPath),
                    parseResult.GetValue(keepContainer),
                    cliPath is null ? null : Path.GetFullPath(cliPath, rootPath)),
                environment.DevcontainerHost ?? DevcontainerHost.Current(),
                output,
                ct);
        });
        return command;
    }

    private static void SetGuardedAction(Command command, Func<ParseResult, TextWriter, TextWriter, CancellationToken, Task<int>> action) =>
        command.SetAction((parseResult, ct) =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;
            return RepoConfigToolApplication.Guard(error, () => action(parseResult, output, error, ct), ct);
        });

    private static int ParseLimit(ArgumentResult result)
    {
        var token = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;
        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit > 0)
        {
            return limit;
        }

        result.AddError($"--limit must be a positive whole number: {token}");
        return DefaultLimit;
    }

    private static void ValidateQueryValue(CommandResult result, Argument<string> query, Argument<string?> value)
    {
        // Read raw tokens: converting the query here would skip its allowed-value validation.
        var queryTokens = result.GetResult(query)?.Tokens;
        var queryName = queryTokens is [var queryToken] ? queryToken.Value : null;
        if (queryName is null || !GetQueries.Contains(queryName, StringComparer.Ordinal))
        {
            return;
        }

        var valueTokens = result.GetResult(value)?.Tokens;
        var valueToken = valueTokens is [var token] ? token.Value : null;
        var valueName = queryName switch
        {
            "blockers-of" => "an item ID",
            "by-tag" => "a tag",
            "by-label" => "a label",
            _ => null,
        };
        if (valueName is not null && valueToken is null)
        {
            result.AddError($"get {queryName} requires {valueName}.");
        }
        else if (valueName is null && valueToken is not null)
        {
            result.AddError($"get {queryName} does not take a value; found '{valueToken}'.");
        }
    }
}
