using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Builds the <c>sharedkernel-repo</c> command tree.
/// </summary>
internal static class RepoConfigCommandLine
{
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
    public static RootCommand Create(RepoConfigToolEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var root = CreateRootOption(environment.WorkingDirectory);
        return new RootCommand("Maintains repository roadmap configuration, GitHub projection, and repository checks.")
        {
            CreateInitCommand(environment, root),
            CreateVerifyCommand(environment, root),
            CreateDiffCommand(environment, root),
            CreateSetCommand(environment, root),
            CreateGetCommand(environment, root),
            CreateGitHubCommand(environment, root),
            CreateAutomationCommand(environment, root),
            CreateVerifyTextEncodingCommand(environment, root),
            CreateVerifyPodmanDnsCommand(environment),
            CreateSecurityCommand(environment, root),
        };
    }

    private static Option<string> CreateRootOption(string workingDirectory) =>
        new("--root")
        {
            Description = "Repository root directory. Defaults to the current directory.",
            HelpName = "path",
            CustomParser = result =>
            {
                var path = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
                if (string.IsNullOrWhiteSpace(path))
                {
                    result.AddError("--root requires a directory path.");
                    return null;
                }

                // A response file can supply a null character, which Path.GetFullPath rejects with an exception that
                // would escape parsing.
                if (path.Contains('\0', StringComparison.Ordinal))
                {
                    result.AddError("--root path contains a null character.");
                    return null;
                }

                return Path.GetFullPath(path, workingDirectory);
            },
        };

    private static Command CreateInitCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Command command = new("init", "Create the missing roadmap structure files. Existing files are kept.") { root };
        SetGuardedAction(command, (parseResult, output, _, _) =>
            Task.FromResult(RepoConfigToolApplication.Init(ResolveRoot(parseResult, root, environment), output)));
        return command;
    }

    private static Command CreateVerifyCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Command command = new("verify", "Verify the roadmap structure, configuration, and items.") { root };
        SetGuardedAction(command, (parseResult, output, error, _) =>
            Task.FromResult(RepoConfigToolApplication.Verify(ResolveRoot(parseResult, root, environment), output, error)));
        return command;
    }

    private static Command CreateDiffCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Command command = new("diff", "Report drift between the roadmap files and the expected structure.") { root };
        SetGuardedAction(command, (parseResult, output, error, _) =>
            Task.FromResult(RepoConfigToolApplication.Diff(ResolveRoot(parseResult, root, environment), output, error)));
        return command;
    }

    private static Command CreateSetCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        string[] keys = [RepoConfigSetter.GitHubRepositoryKey];
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
            ResolveRoot(parseResult, root, environment),
            parseResult.GetRequiredValue(key),
            parseResult.GetRequiredValue(value),
            output)));
        return command;
    }

    private static Command CreateGetCommand(RepoConfigToolEnvironment environment, Option<string> root)
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
            ResolveRoot(parseResult, root, environment),
            parseResult.GetRequiredValue(query),
            parseResult.GetValue(value),
            parseResult.GetValue(type),
            parseResult.GetValue(limit),
            output)));
        return command;
    }

    private static Command CreateGitHubCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Option<bool> force = new("--force")
        {
            Description = "Write the changes instead of previewing them. Validation still applies."
        };

        Command sync = new("sync", "Project roadmap items to GitHub issues and Projects. Previews unless --force is given.") { force, root };
        SetGuardedAction(sync, (parseResult, output, _, ct) => RepoConfigToolApplication.SyncGitHub(
            ResolveRoot(parseResult, root, environment),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        Command reconcile = new("reconcile", "Update the reconciliation manifest from the open GitHub issues. Previews unless --force is given.") { force, root };
        SetGuardedAction(reconcile, (parseResult, output, _, ct) => RepoConfigToolApplication.ReconcileGitHub(
            ResolveRoot(parseResult, root, environment),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        Command import = new("import", "Import the reconciled GitHub issue snapshot into roadmap items. Previews unless --force is given.") { force, root };
        SetGuardedAction(import, (parseResult, output, _, ct) => RepoConfigToolApplication.ImportGitHub(
            ResolveRoot(parseResult, root, environment),
            parseResult.GetValue(force),
            environment.HttpClient,
            output,
            ct));

        return new Command("github", "Preview or write the GitHub projection, reconciliation, and import.") { sync, reconcile, import };
    }

    private static Command CreateAutomationCommand(RepoConfigToolEnvironment environment, Option<string> root) =>
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
        Option<string> root,
        string name,
        string description,
        Func<RoadmapAutomationService, TextWriter, CancellationToken, Task<int>> step)
    {
        Command command = new(name, description) { root };
        SetGuardedAction(command, (parseResult, output, _, ct) => step(
            CreateAutomationService(parseResult, root, environment),
            output,
            ct));
        return command;
    }

    private static Command CreatePublishResultCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Option<bool> force = new("--force")
        {
            Description = "Publish the pull request instead of previewing it. Validation still applies."
        };
        Command command = new("publish-result", "Publish the sealed result as a draft pull request. Previews unless --force is given.") { force, root };
        SetGuardedAction(command, (parseResult, output, _, ct) => RepoConfigToolApplication.PublishAutomationResult(
            CreateAutomationService(parseResult, root, environment),
            parseResult.GetValue(force),
            output,
            ct));
        return command;
    }

    private static RoadmapAutomationService CreateAutomationService(
        ParseResult parseResult,
        Option<string> root,
        RepoConfigToolEnvironment environment) =>
        new(ResolveRoot(parseResult, root, environment), environment.HttpClient, environment.GitRepository);

    private static Command CreateVerifyTextEncodingCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Command command = new("verify-text-encoding", "Verify that text files in the Git index are valid UTF-8 without NUL bytes.") { root };
        SetGuardedAction(command, (parseResult, output, error, ct) =>
            RepoConfigToolApplication.VerifyTextEncoding(ResolveRoot(parseResult, root, environment), output, error, ct));
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
        SetGuardedAction(command, (parseResult, output, error, ct) => PodmanDnsPreflightCommand.Run(
            environment.PodmanHost ?? PodmanDnsHost.Current(),
            parseResult.GetRequiredValue(verbosity) is "q" or "quiet",
            output,
            error,
            ct));
        return command;
    }

    private static Command CreateSecurityCommand(RepoConfigToolEnvironment environment, Option<string> root)
    {
        Command baseline = new("baseline", "Verify repository security document contracts.") { root };
        SetGuardedAction(baseline, (parseResult, output, error, ct) => SecurityCommand.Run(
            ResolveRoot(parseResult, root, environment), null, output, error, ct));
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
        SetGuardedAction(scan, (parseResult, output, error, ct) => SecurityCommand.Run(
            ResolveRoot(parseResult, root, environment), parseResult.GetRequiredValue(destination), output, error, ct));
        return new Command("security", "Verify security document contracts and scan current source.") { baseline, scan };
    }

    private static void SetGuardedAction(Command command, Func<ParseResult, TextWriter, TextWriter, CancellationToken, Task<int>> action) =>
        command.SetAction((parseResult, ct) =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;
            return RepoConfigToolApplication.Guard(error, () => action(parseResult, output, error, ct), ct);
        });

    private static string ResolveRoot(ParseResult parseResult, Option<string> root, RepoConfigToolEnvironment environment) =>
        parseResult.GetValue(root) ?? Path.GetFullPath(environment.WorkingDirectory);

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
