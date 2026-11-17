using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PandaBear.Forgejo;
using PandaBear.InputNormalization;
using PandaBear.Repositories;
using PandaBear.Repositories.Forgejo;
using PandaBear.Repositories.GitHub;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Implements the <c>sharedkernel-repo</c> commands and maps their failures to exit codes.
/// </summary>
internal static class RepoConfigToolApplication
{
    private const decimal ParetoFraction = 0.2m;

    /// <summary>
    /// Runs a command and reports expected failures on standard error with exit code 1.
    /// Caller cancellation propagates so System.CommandLine handles it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> or <paramref name="command"/> is <see langword="null"/>.</exception>
    public static async Task<int> Guard(TextWriter error, Func<Task<int>> command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            return await command().ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            await WriteError(error, exception.Message, ct).ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            await WriteError(error, exception.Message, ct).ConfigureAwait(false);
            return 1;
        }
        catch (HttpRequestException)
        {
            // The transport message can carry request details, so it is never echoed.
            await error.WriteLineAsync("sharedkernel-repo: GitHub sync request failed.".AsMemory(), ct).ConfigureAwait(false);
            return 1;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or JsonException
            or NotSupportedException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            await WriteError(error, exception.Message, ct).ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Creates the missing repository roadmap structure and lists the paths it created.</summary>
    public static int Init(string rootPath, TextWriter output)
    {
        var createdPaths = RoadmapInitializer.Initialize(rootPath);
        if (createdPaths.Count == 0)
        {
            output.WriteLine("Repository roadmap structure is already initialized.");
            return 0;
        }

        output.WriteLine("Initialized repository roadmap structure:");
        foreach (var path in createdPaths)
        {
            output.WriteLine($"- {ControlCharacters.Escape(path)}");
        }

        return 0;
    }

    /// <summary>Verifies the repository configuration and returns 0 when it is valid, otherwise reports the issues and returns 1.</summary>
    public static int Verify(string rootPath, TextWriter output, TextWriter error)
    {
        var issues = RoadmapVerifier.Verify(rootPath);
        if (issues.Count == 0)
        {
            output.WriteLine("Repository config is valid.");
            return 0;
        }

        WriteIssues(error, "Repository config verification failed:", issues);
        return 1;
    }

    /// <summary>Reports repository configuration drift and returns 0 when there is none, otherwise 1.</summary>
    public static int Diff(string rootPath, TextWriter output, TextWriter error)
    {
        var issues = RoadmapVerifier.Verify(rootPath);
        if (issues.Count == 0)
        {
            output.WriteLine("Repository config has no drift.");
            return 0;
        }

        WriteIssues(error, "Repository config drift:", issues);
        return 1;
    }

    /// <summary>Sets one repository configuration value identified by <paramref name="key"/>.</summary>
    public static int Set(string rootPath, string key, string value, TextWriter output)
    {
        RoadmapConfigSetter.Set(rootPath, key, value);
        output.WriteLine($"Updated {ControlCharacters.Escape(key)}.");
        return 0;
    }

    /// <summary>Runs a roadmap query against the loaded roadmap and writes up to <paramref name="limit"/> matching results.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="query"/> is not a known roadmap query.</exception>
    /// <exception cref="InvalidOperationException">The queried roadmap item ID is unknown.</exception>
    public static int Get(string rootPath, string query, string? value, string? type, int limit, TextWriter output)
    {
        // The command tree accepts only known queries and requires a value exactly for blockers-of, by-tag, and by-label.
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
                var itemId = project.Items.FirstOrDefault(item => string.Equals(item.Id, value, StringComparison.Ordinal))?.Id
                    ?? throw new InvalidOperationException($"Unknown roadmap item id: {value}");
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
                WriteItems(output, project.OpenItems(type).Where(item => item.Tags.Contains(value, StringComparer.Ordinal)).OrderByPriority().Take(limit));
                return 0;

            case "by-label":
                WriteItems(output, project.OpenItems(type).Where(item => item.Labels.Contains(value, StringComparer.Ordinal)).OrderByPriority().Take(limit));
                return 0;

            default:
                throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown roadmap query.");
        }
    }

    /// <summary>Previews, or with <paramref name="force"/> applies, the sync of roadmap items to GitHub issues.</summary>
    public static async Task<int> SyncGitHub(string rootPath, bool force, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "sync");
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(rootPath), connection);
        var result = force
            ? await syncer.Apply(ct).ConfigureAwait(false)
            : await syncer.Preview(ct).ConfigureAwait(false);
        await WriteMessages(output, result.Messages, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Previews, or with <paramref name="force"/> applies, the reconciliation of roadmap items with their GitHub issues.</summary>
    public static async Task<int> ReconcileGitHub(string rootPath, bool force, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "reconciliation");
        var reconciler = new GitHubRoadmapReconciler(RoadmapProject.Load(rootPath), connection);
        var messages = force
            ? await reconciler.Apply(ct).ConfigureAwait(false)
            : await reconciler.Preview(ct).ConfigureAwait(false);
        await WriteMessages(output, messages, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Previews, or with <paramref name="force"/> applies, the import of GitHub issues into the roadmap.</summary>
    public static async Task<int> ImportGitHub(string rootPath, bool force, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "import");
        var intake = new GitHubRoadmapIntake(RoadmapProject.Load(rootPath), connection);
        var messages = force
            ? await intake.Apply(ct).ConfigureAwait(false)
            : await intake.Preview(ct).ConfigureAwait(false);
        await WriteMessages(output, messages, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Previews, or with <paramref name="force"/> writes, the configured labels of a GitHub repository.</summary>
    public static async Task<int> SyncLabels(string rootPath, string repository, bool force, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "label sync");
        var store = new GitHubLabelStore(repository, connection);
        await RepositoryLabelSync.Run(rootPath, store, force, output, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Previews, or with <paramref name="force"/> writes, the configured labels of a repository on the Forgejo instance
    /// <paramref name="server"/>. A preview reads a public repository without a token; writing needs <c>FORGEJO_TOKEN</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="force"/> is set without <c>FORGEJO_TOKEN</c>, the repository is not <c>owner/name</c>, the
    /// configuration is invalid, or Forgejo rejects a request.
    /// </exception>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The store owns and disposes the handler.")]
    public static async Task<int> SyncForgejoLabels(
        string rootPath,
        string repository,
        Uri server,
        bool force,
        HttpMessageHandler? handler,
        TextWriter output,
        CancellationToken ct)
    {
        // The CLI names the option; the store's ArgumentException names its parameter.
        if (!ForgejoRepositoryName.TryParse(repository, out _, out _))
        {
            throw new InvalidOperationException("--repository must name a Forgejo repository as owner/name.");
        }

        var token = ForgejoToken.Find(Environment.GetEnvironmentVariable);
        if (force && token is null)
        {
            throw new InvalidOperationException($"Set FORGEJO_TOKEN to a token that may write the labels of {repository}; --force sent no request.");
        }

        handler ??= new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
        ForgejoLabelStore store;
        try
        {
            store = new ForgejoLabelStore(handler, server, repository, new ProductInfoHeaderValue("sharedkernel-repo", "1.0"), token);
        }
        catch
        {
            handler.Dispose();
            throw;
        }

        using (store)
        {
            await RepositoryLabelSync.Run(rootPath, store, force, output, ct).ConfigureAwait(false);
        }

        return 0;
    }

    /// <summary>Previews, or with <paramref name="force"/> creates and verifies, a sub-issue link.</summary>
    public static async Task<int> LinkSubissue(string repository, int parent, int child, bool force, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "subissue linking");
        await GitHubSubissueLinker.Run(repository, parent, child, force, connection, output, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Writes the dependency snapshot and returns 0, or reports why it cannot and returns 1.</summary>
    public static async Task<int> WriteDependencySnapshot(DependencySnapshotOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (!await GitHubDependencySnapshot.TryWrite(options, ct).ConfigureAwait(false))
        {
            await error.WriteLineAsync("dependency snapshot: Invalid arguments, unsafe paths, incomplete committed dependency inputs, or dependency size limits exceeded.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 1;
        }

        await output.WriteLineAsync("Dependency snapshot generated from committed lockfiles without package restore.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Previews, or with <see cref="DependencySubmitOptions.Force"/> submits, a dependency snapshot.</summary>
    public static async Task<int> SubmitDependencySnapshot(DependencySubmitOptions options, HttpClient? httpClient, TextWriter output, CancellationToken ct)
    {
        using var connection = GitHubConnections.FromEnvironment(httpClient, "dependency submission");
        await GitHubDependencySubmission.Run(options, connection, output, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Verifies the encoding of repository text files and returns 0 when valid, otherwise reports issues and returns 1.</summary>
    public static async Task<int> VerifyTextEncoding(string rootPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await TextEncodingVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Repository text encoding is valid.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Repository text encoding verification failed:", issues);
        return 1;
    }

    /// <summary>Verifies that repository JSON files are valid and returns 0 when they are, otherwise reports issues and returns 1.</summary>
    public static async Task<int> VerifyJson(string rootPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await JsonVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Repository JSON is valid.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Repository JSON verification failed:", issues);
        return 1;
    }

    /// <summary>Verifies that repository line endings match their attributes and returns 0 when they do, otherwise reports issues and returns 1.</summary>
    public static async Task<int> VerifyLineEndings(string rootPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await LineEndingVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Repository line endings match their attributes.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Repository line-ending verification failed:", issues);
        return 1;
    }

    /// <summary>Verifies the pinned third-party Actions against the manifest and returns 0 when they match, otherwise reports issues and returns 1.</summary>
    public static async Task<int> VerifyActionPins(string rootPath, string? manifestPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await ActionPinVerifier.Verify(rootPath, manifestPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Action pins match the manifest.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Action pin verification failed:", issues);
        return 1;
    }

    /// <summary>Verifies that every Dependabot ignore entry names update-types and returns 0 when they do, otherwise reports issues and returns 1.</summary>
    public static async Task<int> VerifyDependabot(string rootPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await DependabotIgnoreVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Dependabot ignore entries leave security updates enabled.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Dependabot ignore verification failed:", issues);
        return 1;
    }

    /// <summary>
    /// Verifies the links in the documentation files that the <c>links</c> section of the consumer configuration
    /// selects, or in <paramref name="paths"/> when any are given.
    /// </summary>
    /// <exception cref="InvalidOperationException">The consumer configuration has no links section.</exception>
    public static async Task<int> VerifyLinks(string rootPath, IReadOnlyList<string> paths, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var (fileCount, issues) = await LinkVerifier.Verify(rootPath, paths, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Link validation passed for {fileCount} documentation files.").AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Link validation failed:", issues);
        return 1;
    }

    /// <summary>
    /// Verifies the feature files that the <c>gherkin</c> section of the consumer configuration selects, or the tracked
    /// feature files that <paramref name="paths"/> name, against its rules.
    /// </summary>
    /// <exception cref="InvalidOperationException">The consumer configuration has no gherkin section.</exception>
    public static async Task<int> VerifyGherkin(string rootPath, IReadOnlyList<string> paths, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await GherkinVerifier.Verify(rootPath, paths, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Repository Gherkin files are valid.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Repository Gherkin verification failed:", issues);
        return 1;
    }

    /// <summary>
    /// Verifies the target frameworks of the tracked project files against the <c>targetFrameworks</c> section of the
    /// consumer configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">The consumer configuration is invalid or has no targetFrameworks section.</exception>
    public static async Task<int> VerifyTargetFrameworks(string rootPath, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var issues = await TargetFrameworkVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count == 0)
        {
            await output.WriteLineAsync("Target frameworks match the configured policy.".AsMemory(), ct).ConfigureAwait(false);
            return 0;
        }

        WriteIssues(error, "Target framework verification failed:", issues);
        return 1;
    }

    /// <summary>
    /// Verifies the license exceptions in the consumer configuration. On success, appends
    /// <c>packages=pkg:nuget/&lt;id&gt;@&lt;version&gt;,...</c> to <paramref name="githubOutput"/> when it is given.
    /// </summary>
    /// <exception cref="InvalidOperationException">The consumer configuration has no licenseExceptions section.</exception>
    public static async Task<int> VerifyLicenseExceptions(string rootPath, string? githubOutput, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var (issues, packageUrls) = await LicenseExceptionVerifier.Verify(rootPath, ct).ConfigureAwait(false);
        if (issues.Count > 0)
        {
            WriteIssues(error, "License exception verification failed:", issues);
            return 1;
        }

        var packages = string.Join(',', packageUrls);
        if (githubOutput is not null)
        {
            await File.AppendAllTextAsync(Path.GetFullPath(githubOutput, rootPath), "packages=" + packages + Environment.NewLine, ct).ConfigureAwait(false);
        }

        await output.WriteLineAsync($"License exceptions are current: {packages}".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Creates the roadmap automation plan for the current workflow run and publishes its digest, branch name, and base SHA as workflow outputs.</summary>
    /// <exception cref="InvalidOperationException"><c>GITHUB_RUN_ID</c> is not a positive number.</exception>
    public static async Task<int> PlanAutomation(RoadmapAutomationService service, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        var runIdValue = RequireEnvironmentVariable("GITHUB_RUN_ID");
        if (!long.TryParse(runIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var runId) || runId < 1)
        {
            throw new InvalidOperationException("Roadmap automation requires a positive GITHUB_RUN_ID.");
        }

        var plan = await service.CreatePlan(runId, repository, ct).ConfigureAwait(false);
        await AppendWorkflowOutput(
            [
                ("plan_digest", plan.PlanDigest),
                ("has_changes", FormatBoolean(plan.HasChanges)),
                ("branch_name", plan.BranchName),
                ("base_sha", plan.BaseSha)
            ],
            ct).ConfigureAwait(false);
        await AppendWorkflowSummary(
            "Roadmap maintenance plan",
            [
                ("Plan digest", plan.PlanDigest),
                ("Base SHA", plan.BaseSha),
                ("Proposed changes", plan.HasChanges ? "yes" : "no")
            ],
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation plan created.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Validates the roadmap automation plan identified by the plan digest environment variable.</summary>
    public static async Task<int> ValidateAutomationPlan(RoadmapAutomationService service, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        await service.ValidatePlan(
            RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
            repository,
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation plan validated.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Applies the closure approvals of the validated roadmap automation plan.</summary>
    public static async Task<int> ApplyAutomationPlan(RoadmapAutomationService service, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        await service.ApplyPlan(
            RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
            repository,
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation closure approvals applied.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Seals the roadmap automation result for the validated plan and publishes the seal as workflow output.</summary>
    public static async Task<int> SealAutomationResult(RoadmapAutomationService service, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        var seal = await service.SealResult(
            RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
            RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST"),
            repository,
            ct).ConfigureAwait(false);
        await AppendWorkflowOutput(
            [
                ("result_digest", seal.ResultDigest),
                ("has_changes", FormatBoolean(seal.HasChanges)),
                ("path_count", seal.PathCount.ToString(CultureInfo.InvariantCulture))
            ],
            ct).ConfigureAwait(false);
        await AppendWorkflowSummary(
            "Roadmap maintenance result",
            [
                ("Result digest", seal.ResultDigest),
                ("Changed paths", seal.PathCount.ToString(CultureInfo.InvariantCulture)),
                ("Changes retained", seal.HasChanges ? "yes" : "no")
            ],
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation result sealed.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Verifies the sealed roadmap automation result for the plan identified by the plan digest environment variable.</summary>
    public static async Task<int> VerifyAutomationResult(RoadmapAutomationService service, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        await service.VerifyResult(
            RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST"),
            RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST"),
            RequireEnvironmentVariable("ROADMAP_RESULT_DIGEST"),
            repository,
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation result verified.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Publishes the sealed result as a draft pull request when <paramref name="force"/> is set. Otherwise runs the same
    /// validation and prints the repository, base SHA, branch, and changed paths without changing GitHub or local files.
    /// </summary>
    public static async Task<int> PublishAutomationResult(RoadmapAutomationService service, bool force, TextWriter output, CancellationToken ct)
    {
        var repository = RequireEnvironmentVariable("GITHUB_REPOSITORY");
        var planDigest = RequireEnvironmentVariable("ROADMAP_PLAN_DIGEST");
        var artifactDigest = RequireEnvironmentVariable("ROADMAP_ARTIFACT_DIGEST");
        var resultDigest = RequireEnvironmentVariable("ROADMAP_RESULT_DIGEST");
        if (!force)
        {
            var preview = await service.PreviewResult(planDigest, artifactDigest, resultDigest, repository, ct).ConfigureAwait(false);
            await WriteMessages(
                output,
                [
                    $"preview: roadmap publication would create branch {preview.BranchName} from {preview.BaseSha} in {preview.Repository} and open a draft pull request.",
                    .. preview.Paths.Select(path => $"preview: roadmap publication would change {path}."),
                ],
                ct).ConfigureAwait(false);
            return 0;
        }

        // Without these files a published pull request would go unreported, so they are required before any write.
        _ = RequireEnvironmentVariable("GITHUB_OUTPUT");
        _ = RequireEnvironmentVariable("GITHUB_STEP_SUMMARY");
        var publication = await service.PublishResult(
            planDigest,
            artifactDigest,
            resultDigest,
            repository,
            ct).ConfigureAwait(false);
        await AppendWorkflowOutput(
            [
                ("pull_request_url", publication.PullRequestUrl.OriginalString),
                ("publication_commit", publication.CommitSha)
            ],
            ct).ConfigureAwait(false);
        await AppendWorkflowSummary(
            "Roadmap maintenance publication",
            [
                ("Pull request", publication.PullRequestUrl.OriginalString),
                ("Commit SHA", publication.CommitSha)
            ],
            ct).ConfigureAwait(false);
        await output.WriteLineAsync("Roadmap automation pull request published.".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }

    private static Task WriteError(TextWriter error, string message, CancellationToken ct) =>
        error.WriteLineAsync($"sharedkernel-repo: {ControlCharacters.Escape(message)}".AsMemory(), ct);

    private static async Task WriteMessages(TextWriter output, IEnumerable<string> messages, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            await output.WriteLineAsync(ControlCharacters.Escape(message).AsMemory(), ct).ConfigureAwait(false);
        }
    }

    private static void WriteIssues(TextWriter error, string heading, IReadOnlyCollection<VerificationIssue> issues)
    {
        error.WriteLine(heading);
        foreach (var issue in issues)
        {
            error.WriteLine($"- {ControlCharacters.Escape(issue.Path)}: {ControlCharacters.Escape(issue.Message)}");
        }
    }

    private static void WriteItems(TextWriter output, IEnumerable<RoadmapItemSnapshot> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            var id = ControlCharacters.Escape(item.Id);
            var type = ControlCharacters.Escape(item.Type);
            var status = ControlCharacters.Escape(item.Status);
            var title = ControlCharacters.Escape(item.Title);
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
            output.WriteLine($"{ControlCharacters.Escape(item.Key)} | {item.Value.ToString(CultureInfo.InvariantCulture)}");
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
            output.WriteLine($"{ControlCharacters.Escape(item.Item.Id)} blocked by {string.Join(", ", item.Blockers.Select(ControlCharacters.Escape))}");
            count++;
        }

        if (count == 0)
        {
            output.WriteLine("No blocked roadmap items.");
        }
    }

    private static async Task AppendWorkflowOutput(
        IEnumerable<(string Name, string Value)> values,
        CancellationToken ct)
    {
        var path = RequireEnvironmentVariable("GITHUB_OUTPUT");
        var content = string.Join(
            Environment.NewLine,
            values.Select(value => $"{value.Name}={value.Value}")) + Environment.NewLine;
        await File.AppendAllTextAsync(path, content, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task AppendWorkflowSummary(
        string heading,
        IEnumerable<(string Name, string Value)> values,
        CancellationToken ct)
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
        await File.AppendAllTextAsync(path, content.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
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
