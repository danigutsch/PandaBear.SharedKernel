using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

internal static class RepoConfigToolApplication
{
    private const decimal ParetoFraction = 0.2m;
    private const string Usage = "Usage: sharedkernel-repo <init|verify|diff|security|text-encoding|podman-dns-preflight|set|get|sync|reconcile|intake|automation> [--root <path>]";

    public static Task<int> Run(string[] args, TextWriter output, TextWriter error, string workingDirectory, CancellationToken cancellationToken) =>
        Run(args, output, error, workingDirectory, httpClient: null, gitRepository: null, cancellationToken);

    internal static Task<int> Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        CancellationToken cancellationToken) =>
        Run(args, output, error, workingDirectory, httpClient, gitRepository: null, cancellationToken);

    internal static async Task<int> Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        RoadmapGitRepository? gitRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        if (args is [] or ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(Usage.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("Commands: init, verify, diff, security <baseline|scan>, text-encoding, podman-dns-preflight, set github.repository <owner/repo>, get <query>, sync github, reconcile github, intake github, automation <command>.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "security" => await SecurityCommand.Run(args[1..], output, error, workingDirectory, cancellationToken).ConfigureAwait(false),
                "sync" => await RunGitHubProjection(args[1..], output, error, workingDirectory, httpClient, cancellationToken).ConfigureAwait(false),
                "reconcile" => await RunGitHubReconciliation(args[1..], output, error, workingDirectory, httpClient, cancellationToken).ConfigureAwait(false),
                "intake" => await RunGitHubIntake(args[1..], output, error, workingDirectory, httpClient, cancellationToken).ConfigureAwait(false),
                "podman-dns-preflight" => await PodmanDnsPreflightCommand.Run(args[1..], output, error, cancellationToken).ConfigureAwait(false),
                "automation" => await RunRoadmapAutomation(args[1..], output, error, workingDirectory, httpClient, gitRepository, cancellationToken).ConfigureAwait(false),
                _ => await RunCommand(args, output, error, workingDirectory, cancellationToken).ConfigureAwait(false)
            };
        }
        catch (TimeoutException exception)
        {
            await error.WriteLineAsync($"sharedkernel-repo: {EscapeControlCharacters(exception.Message)}".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync($"sharedkernel-repo: {EscapeControlCharacters(exception.Message)}".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (HttpRequestException)
        {
            await error.WriteLineAsync("sharedkernel-repo: GitHub sync request failed.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or JsonException
            or NotSupportedException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            await error.WriteLineAsync($"sharedkernel-repo: {EscapeControlCharacters(exception.Message)}".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    private static Task<int> RunCommand(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var command = args[0];
        return command == "text-encoding"
            ? RunTextEncoding(args[1..], output, error, workingDirectory, cancellationToken)
            : Task.FromResult(command switch
            {
                "init" => RunInit(args[1..], output, error, workingDirectory),
                "verify" => RunVerify(args[1..], output, error, workingDirectory),
                "diff" => RunDiff(args[1..], output, error, workingDirectory),
                "set" => RunSet(args[1..], output, error, workingDirectory),
                "get" => RunGet(args[1..], output, error, workingDirectory),
                _ => WriteUsageError(error, $"Unknown command: {command}")
            });
    }

    private static async Task<int> RunTextEncoding(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return 2;
        }

        if (remaining.Length > 0)
        {
            return WriteUsageError(error, $"Unknown argument: {remaining[0]}");
        }

        var issues = await TextEncodingVerifier.Verify(rootPath, cancellationToken).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Repository text encoding is valid.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Repository text encoding verification failed:", issues);
        return 1;
    }

    private static int RunInit(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining) || remaining.Length > 0)
        {
            return 2;
        }

        var createdPaths = RepoConfigInitializer.Initialize(rootPath);
        if (createdPaths.Count == 0)
        {
            output.WriteLine("Repository roadmap structure is already initialized.");
            return 0;
        }

        output.WriteLine("Initialized repository roadmap structure:");
        foreach (var path in createdPaths)
        {
            output.WriteLine($"- {EscapeControlCharacters(path)}");
        }

        return 0;
    }

    private static int RunVerify(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining) || remaining.Length > 0)
        {
            return 2;
        }

        var issues = RepoConfigVerifier.Verify(rootPath);
        if (issues.Count == 0)
        {
            output.WriteLine("Repository config is valid.");
            return 0;
        }

        WriteIssues(error, "Repository config verification failed:", issues);
        return 1;
    }

    private static int RunDiff(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining) || remaining.Length > 0)
        {
            return 2;
        }

        var issues = RepoConfigVerifier.Verify(rootPath);
        if (issues.Count == 0)
        {
            output.WriteLine("Repository config has no drift.");
            return 0;
        }

        WriteIssues(error, "Repository config drift:", issues);
        return 1;
    }

    private static int RunSet(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return 2;
        }

        if (remaining.Length < 2)
        {
            return WriteUsageError(error, "Missing set key and value.");
        }

        if (remaining.Length > 2)
        {
            return WriteUsageError(error, $"Unknown argument: {remaining[2]}");
        }

        var key = remaining[0];
        var value = remaining[1];

        RepoConfigSetter.Set(rootPath, key, value);
        output.WriteLine($"Updated {EscapeControlCharacters(key)}.");
        return 0;
    }

    private static int RunGet(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return 2;
        }

        if (remaining.Length == 0)
        {
            return WriteUsageError(error, "Missing get query.");
        }

        var query = remaining[0];
        if (!TryParseGetOptions(remaining[1..], error, out var positionalArgs, out var type, out var limit))
        {
            return 2;
        }

        if (!IsGetQueryShapeValid(query, positionalArgs))
        {
            return WriteUsageError(error, $"Unknown or incomplete get query: {query}");
        }

        var project = RoadmapProject.Load(rootPath);

        switch (query)
        {
            case "next-priority":
                WriteItems(output, project.OpenItems(type).OrderByPriority().Take(limit));
                return 0;

            case "next-unblocked":
                WriteItems(output, project.OpenItems(type).Where(project.IsUnblocked).OrderByPriority().Take(limit));
                return 0;

            case "next-work":
                var openItems = project.OpenItems();
                var blockerIdsWithOpenDependents = openItems.SelectMany(item => item.BlockedBy).ToHashSet(StringComparer.Ordinal);
                WriteItems(
                    output,
                    openItems
                        .Where(item => (type is null || string.Equals(item.Type, type, StringComparison.Ordinal)) && project.IsUnblocked(item))
                        .OrderByDescending(candidate => blockerIdsWithOpenDependents.Contains(candidate.Id))
                        .ThenBy(item => item.Order)
                        .ThenByDescending(item => item.Score)
                        .ThenBy(item => item.Id, StringComparer.Ordinal)
                        .Take(limit));
                return 0;

            case "blockers-of":
                var itemId = positionalArgs[0];
                if (!project.Items.Any(item => string.Equals(item.Id, itemId, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException($"Unknown roadmap item id: {itemId}");
                }

                WriteItems(output, project.BlockersOf(itemId).OrderByPriority().Take(limit));
                return 0;

            case "next-blockers":
                WriteItems(output, project.OpenItems(type).Where(item => item.Type == "blocker" || item.Blocks.Count > 0).OrderByDescending(item => item.Blocks.Count).ThenBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).Take(limit));
                return 0;

            case "next-enablers":
                WriteItems(output, project.OpenItems("enabler").Where(project.IsUnblocked).OrderByPriority().Take(limit));
                return 0;

            case "low-hanging-fruit":
                WriteItems(output, project.OpenItems(type).Where(project.IsUnblocked).OrderBy(item => item.Effort).ThenByDescending(item => item.Score).ThenBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).Take(limit));
                return 0;

            case "pareto":
                var unblockedOpenItems = project.OpenItems(type).Where(project.IsUnblocked).ToArray();
                var paretoLimit = Math.Max(1, (int)Math.Ceiling(unblockedOpenItems.Length * ParetoFraction));
                WriteItems(output, unblockedOpenItems.OrderByDescending(item => item.Score).ThenBy(item => item.Effort).ThenBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).Take(Math.Min(limit, paretoLimit)));
                return 0;

            case "blocking-overview":
                WriteBlockingOverview(output, project.OpenItems(type).OrderByPriority(), project);
                return 0;

            case "tags":
                WriteCounts(output, project.TagCounts());
                return 0;

            case "labels":
                WriteCounts(output, project.LabelCounts());
                return 0;

            case "by-tag":
                var tag = positionalArgs[0];
                WriteItems(output, project.OpenItems(type).Where(item => item.Tags.Contains(tag, StringComparer.Ordinal)).OrderByPriority().Take(limit));
                return 0;

            case "by-label":
                var label = positionalArgs[0];
                WriteItems(output, project.OpenItems(type).Where(item => item.Labels.Contains(label, StringComparer.Ordinal)).OrderByPriority().Take(limit));
                return 0;

            default:
                return WriteUsageError(error, $"Unknown or incomplete get query: {query}");
        }
    }

    private static async Task<int> RunGitHubProjection(string[] args, TextWriter output, TextWriter error, string workingDirectory, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        var parsed = TryParseGitHubProjection(args, workingDirectory, error);
        if (parsed is null)
        {
            return 2;
        }

        var (rootPath, dryRun) = parsed.Value;
        var project = RoadmapProject.Load(rootPath);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);
        var result = dryRun
            ? await syncer.Preview(cancellationToken).ConfigureAwait(false)
            : await syncer.Apply(cancellationToken).ConfigureAwait(false);
        foreach (var message in result.Messages)
        {
            await output.WriteLineAsync(EscapeControlCharacters(message).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> RunGitHubIntake(string[] args, TextWriter output, TextWriter error, string workingDirectory, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        var parsed = TryParseGitHubIntake(args, workingDirectory, error);
        if (parsed is null)
        {
            return 2;
        }

        var (rootPath, dryRun) = parsed.Value;
        var project = RoadmapProject.Load(rootPath);
        var intake = new GitHubRoadmapIntake(project, httpClient);
        var messages = dryRun
            ? await intake.Preview(cancellationToken).ConfigureAwait(false)
            : await intake.Apply(cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await output.WriteLineAsync(EscapeControlCharacters(message).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> RunGitHubReconciliation(string[] args, TextWriter output, TextWriter error, string workingDirectory, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        var parsed = TryParseGitHubReconciliation(args, workingDirectory, error);
        if (parsed is null)
        {
            return 2;
        }

        var (rootPath, dryRun) = parsed.Value;
        var project = RoadmapProject.Load(rootPath);
        var reconciler = new GitHubRoadmapReconciler(project, httpClient);
        var messages = dryRun
            ? await reconciler.Preview(cancellationToken).ConfigureAwait(false)
            : await reconciler.Apply(cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await output.WriteLineAsync(EscapeControlCharacters(message).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> RunRoadmapAutomation(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        RoadmapGitRepository? gitRepository,
        CancellationToken cancellationToken)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return 2;
        }

        if (remaining.Length == 0)
        {
            return WriteUsageError(error, "Missing automation command.");
        }

        if (remaining.Length > 1)
        {
            return WriteUsageError(error, $"Unknown automation argument: {remaining[1]}");
        }

        if (remaining[0] is not "plan-github"
            and not "validate-plan"
            and not "apply-plan"
            and not "seal-result"
            and not "verify-result"
            and not "publish-result")
        {
            return WriteUsageError(error, $"Unknown automation command: {remaining[0]}");
        }

        var service = new RoadmapAutomationService(rootPath, httpClient, gitRepository);
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        switch (remaining[0])
        {
            case "plan-github":
                var runIdValue = RequireEnvironmentVariable("GITHUB_RUN_ID");
                if (!long.TryParse(runIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var runId) || runId < 1)
                {
                    throw new InvalidOperationException("Roadmap automation requires a positive GITHUB_RUN_ID.");
                }

                var plan = await service.CreatePlan(runId, repository, cancellationToken).ConfigureAwait(false);
                await AppendWorkflowOutput(
                    [
                        ("plan_digest", plan.PlanDigest),
                        ("has_changes", FormatBoolean(plan.HasChanges)),
                        ("branch_name", plan.BranchName),
                        ("base_sha", plan.BaseSha)
                    ],
                    cancellationToken).ConfigureAwait(false);
                await AppendWorkflowSummary(
                    "Roadmap maintenance plan",
                    [
                        ("Plan digest", plan.PlanDigest),
                        ("Base SHA", plan.BaseSha),
                        ("Proposed changes", plan.HasChanges ? "yes" : "no")
                    ],
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation plan created.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            case "validate-plan":
                await service.ValidatePlan(
                    RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
                    repository,
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation plan validated.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            case "apply-plan":
                await service.ApplyPlan(
                    RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
                    repository,
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation closure approvals applied.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            case "seal-result":
                var seal = await service.SealResult(
                    RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
                    RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST"),
                    repository,
                    cancellationToken).ConfigureAwait(false);
                await AppendWorkflowOutput(
                    [
                        ("result_digest", seal.ResultDigest),
                        ("has_changes", FormatBoolean(seal.HasChanges)),
                        ("path_count", seal.PathCount.ToString(CultureInfo.InvariantCulture))
                    ],
                    cancellationToken).ConfigureAwait(false);
                await AppendWorkflowSummary(
                    "Roadmap maintenance result",
                    [
                        ("Result digest", seal.ResultDigest),
                        ("Changed paths", seal.PathCount.ToString(CultureInfo.InvariantCulture)),
                        ("Changes retained", seal.HasChanges ? "yes" : "no")
                    ],
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation result sealed.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            case "verify-result":
                await service.VerifyResult(
                    RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
                    RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST"),
                    RequireEnvironmentVariable("ROADMAP_RESULT_DIGEST"),
                    repository,
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation result verified.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            case "publish-result":
                var publication = await service.PublishResult(
                    RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
                    RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST"),
                    RequireEnvironmentVariable("ROADMAP_RESULT_DIGEST"),
                    repository,
                    cancellationToken).ConfigureAwait(false);
                await AppendWorkflowOutput(
                    [
                        ("pull_request_url", publication.PullRequestUrl),
                        ("publication_commit", publication.CommitSha)
                    ],
                    cancellationToken).ConfigureAwait(false);
                await AppendWorkflowSummary(
                    "Roadmap maintenance publication",
                    [
                        ("Pull request", publication.PullRequestUrl),
                        ("Commit SHA", publication.CommitSha)
                    ],
                    cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Roadmap automation pull request published.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;

            default:
                return WriteUsageError(error, $"Unknown automation command: {remaining[0]}");
        }
    }

    private static (string RootPath, bool DryRun)? TryParseGitHubProjection(string[] args, string workingDirectory, TextWriter error)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return null;
        }

        if (remaining.Length == 0 || !string.Equals(remaining[0], "github", StringComparison.Ordinal))
        {
            WriteUsageError(error, "Missing sync target: github.");
            return null;
        }

        var dryRun = !remaining.Contains("--apply", StringComparer.Ordinal);
        if (remaining.Contains("--dry-run", StringComparer.Ordinal) && remaining.Contains("--apply", StringComparer.Ordinal))
        {
            WriteUsageError(error, "Use either --dry-run or --apply, not both.");
            return null;
        }

        if (remaining.Any(argument => argument is not "github" and not "--dry-run" and not "--apply"))
        {
            WriteUsageError(error, "Unknown sync argument.");
            return null;
        }

        return (rootPath, dryRun);
    }

    private static (string RootPath, bool DryRun)? TryParseGitHubIntake(string[] args, string workingDirectory, TextWriter error)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return null;
        }

        if (remaining.Length == 0 || !string.Equals(remaining[0], "github", StringComparison.Ordinal))
        {
            WriteUsageError(error, "Missing intake target: github.");
            return null;
        }

        var dryRun = !remaining.Contains("--apply", StringComparer.Ordinal);
        if (remaining.Contains("--dry-run", StringComparer.Ordinal) && remaining.Contains("--apply", StringComparer.Ordinal))
        {
            WriteUsageError(error, "Use either --dry-run or --apply, not both.");
            return null;
        }

        if (remaining.Any(argument => argument is not "github" and not "--dry-run" and not "--apply"))
        {
            WriteUsageError(error, "Unknown intake argument.");
            return null;
        }

        return (rootPath, dryRun);
    }

    private static (string RootPath, bool DryRun)? TryParseGitHubReconciliation(string[] args, string workingDirectory, TextWriter error)
    {
        if (!TryParseRootOption(args, workingDirectory, error, out var rootPath, out var remaining))
        {
            return null;
        }

        if (remaining.Length == 0 || !string.Equals(remaining[0], "github", StringComparison.Ordinal))
        {
            WriteUsageError(error, "Missing reconciliation target: github.");
            return null;
        }

        var dryRun = !remaining.Contains("--apply", StringComparer.Ordinal);
        if (remaining.Contains("--dry-run", StringComparer.Ordinal) && remaining.Contains("--apply", StringComparer.Ordinal))
        {
            WriteUsageError(error, "Use either --dry-run or --apply, not both.");
            return null;
        }

        if (remaining.Any(argument => argument is not "github" and not "--dry-run" and not "--apply"))
        {
            WriteUsageError(error, "Unknown reconciliation argument.");
            return null;
        }

        return (rootPath, dryRun);
    }

    private static bool TryParseRootOption(string[] args, string workingDirectory, TextWriter error, out string rootPath, out string[] remaining)
    {
        rootPath = Path.GetFullPath(workingDirectory);
        List<string> remainingArgs = [];
        var index = 0;
        while (index < args.Length)
        {
            if (args[index] == "--root")
            {
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    remaining = [];
                    WriteUsageError(error, "Missing required value for --root.");
                    return false;
                }

                rootPath = Path.GetFullPath(args[index + 1], workingDirectory);
                index += 2;
                continue;
            }

            remainingArgs.Add(args[index]);
            index++;
        }

        remaining = [.. remainingArgs];
        return true;
    }

    private static int WriteUsageError(TextWriter error, string message)
    {
        error.WriteLine(EscapeControlCharacters(message));
        error.WriteLine(Usage);
        return 2;
    }

    private static void WriteIssues(TextWriter error, string heading, IReadOnlyCollection<RepoConfigIssue> issues)
    {
        error.WriteLine(heading);
        foreach (var issue in issues)
        {
            error.WriteLine($"- {EscapeControlCharacters(issue.Path)}: {EscapeControlCharacters(issue.Message)}");
        }
    }

    private static bool TryParseGetOptions(string[] args, TextWriter error, out string[] positionalArgs, out string? type, out int limit)
    {
        List<string> positionalValues = [];
        type = null;
        limit = 10;
        var index = 0;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "--type" when HasOptionValue(args, index):
                    type = args[index + 1];
                    index += 2;
                    continue;

                case "--type":
                    WriteUsageError(error, "Missing required value for --type.");
                    positionalArgs = [];
                    return false;

                case "--limit" when HasOptionValue(args, index) && int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLimit) && parsedLimit > 0:
                    limit = parsedLimit;
                    index += 2;
                    continue;

                case "--limit":
                    WriteUsageError(error, "Missing or invalid required value for --limit.");
                    positionalArgs = [];
                    return false;

                default:
                    if (args[index].StartsWith("--", StringComparison.Ordinal))
                    {
                        WriteUsageError(error, $"Unknown get option: {args[index]}");
                        positionalArgs = [];
                        return false;
                    }

                    positionalValues.Add(args[index]);
                    index++;
                    continue;
            }
        }

        positionalArgs = [.. positionalValues];
        return true;
    }

    private static bool HasOptionValue(string[] args, int index) =>
        index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal);

    private static bool IsGetQueryShapeValid(string query, string[] positionalArgs) =>
        query switch
        {
            "next-priority" or "next-unblocked" or "next-work" or "next-blockers" or "next-enablers" or "low-hanging-fruit" or "pareto" or "blocking-overview" or "tags" or "labels" => positionalArgs.Length == 0,
            "blockers-of" or "by-tag" or "by-label" => positionalArgs.Length == 1,
            _ => false
        };

    private static void WriteItems(TextWriter output, IEnumerable<RoadmapItemSnapshot> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            var id = EscapeControlCharacters(item.Id);
            var type = EscapeControlCharacters(item.Type);
            var status = EscapeControlCharacters(item.Status);
            var title = EscapeControlCharacters(item.Title);
            output.WriteLine(item.IsTriaged
                ? $"{id} | {type} | {status} | order {item.Order?.ToString(CultureInfo.InvariantCulture)} | score {item.Score?.ToString("0.##", CultureInfo.InvariantCulture)} | {title}"
                : $"{id} | {type} | {status} | untriaged | {title}");
            count++;
        }

        if (count == 0)
        {
            output.WriteLine("No matching roadmap items.");
        }
    }

    private static void WriteCounts(TextWriter output, IEnumerable<KeyValuePair<string, int>> counts)
    {
        var count = 0;
        foreach (var item in counts)
        {
            output.WriteLine($"{EscapeControlCharacters(item.Key)} | {item.Value.ToString(CultureInfo.InvariantCulture)}");
            count++;
        }

        if (count == 0)
        {
            output.WriteLine("No matching values.");
        }
    }

    private static void WriteBlockingOverview(TextWriter output, IEnumerable<RoadmapItemSnapshot> items, RoadmapProject project)
    {
        var count = 0;
        var blockedItems = items
            .Select(item => new
            {
                Item = item,
                Blockers = project.BlockersOf(item.Id).Where(blocker => !project.IsClosed(blocker)).Select(blocker => blocker.Id).ToArray()
            })
            .Where(item => item.Blockers.Length > 0);

        foreach (var item in blockedItems)
        {
            output.WriteLine($"{EscapeControlCharacters(item.Item.Id)} blocked by {string.Join(", ", item.Blockers.Select(EscapeControlCharacters))}");
            count++;
        }

        if (count == 0)
        {
            output.WriteLine("No blocked roadmap items.");
        }
    }

    internal static string EscapeControlCharacters(string value)
    {
        StringBuilder? escaped = null;
        var index = 0;
        while (index < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                escaped ??= new StringBuilder(value.Length + 6).Append(value, 0, index);
                escaped.Append("\\u").Append(((int)value[index]).ToString("X4", CultureInfo.InvariantCulture));
                index++;
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is not UnicodeCategory.Control
                    and not UnicodeCategory.Format
                    and not UnicodeCategory.LineSeparator
                    and not UnicodeCategory.ParagraphSeparator)
            {
                escaped?.Append(value, index, consumed);
                index += consumed;
                continue;
            }

            escaped ??= new StringBuilder(value.Length + 6).Append(value, 0, index);
            escaped
                .Append(rune.IsBmp ? "\\u" : "\\U")
                .Append(rune.Value.ToString(rune.IsBmp ? "X4" : "X8", CultureInfo.InvariantCulture));
            index += consumed;
        }

        return escaped?.ToString() ?? value;
    }

    private static async Task AppendWorkflowOutput(
        IEnumerable<(string Name, string Value)> values,
        CancellationToken cancellationToken)
    {
        var path = RequireEnvironmentVariable("GITHUB_OUTPUT");
        var content = string.Join(
            Environment.NewLine,
            values.Select(value => $"{value.Name}={value.Value}")) + Environment.NewLine;
        await File.AppendAllTextAsync(path, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AppendWorkflowSummary(
        string heading,
        IEnumerable<(string Name, string Value)> values,
        CancellationToken cancellationToken)
    {
        var path = RequireEnvironmentVariable("GITHUB_STEP_SUMMARY");
        var content = new StringBuilder()
            .Append("## ").AppendLine(heading)
            .AppendLine();
        foreach (var value in values)
        {
            content.Append("- ").Append(value.Name).Append(": `").Append(value.Value).AppendLine("`");
        }

        content.AppendLine();
        await File.AppendAllTextAsync(path, content.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatBoolean(bool value) => value ? "true" : "false";

    private static string RequireEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Roadmap automation requires {name}.");
        }

        return value;
    }
}
