namespace PandaBear.RepoConfig.Tool;

/// <summary>Runs the roadmap maintenance workflow steps: plan, validate, apply, seal, verify, and publish.</summary>
internal sealed class RoadmapAutomationService
{
    private const string PlanRelativePath = "artifacts/roadmap-maintenance/roadmap-plan.json";
    private const string ResultSealRelativePath = "artifacts/roadmap-maintenance/result-seal.json";
    private readonly RoadmapGitRepository _gitRepository;
    private readonly HttpClient? _httpClient;
    private readonly string _rootPath;

    /// <summary>Creates the service for the checkout at <paramref name="rootPath"/>, optionally with injected HTTP and git dependencies.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    public RoadmapAutomationService(
        string rootPath,
        HttpClient? httpClient = null,
        RoadmapGitRepository? gitRepository = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _httpClient = httpClient;
        _gitRepository = gitRepository ?? new RoadmapGitRepository();
    }

    /// <summary>Returns the full path of the plan artifact under <paramref name="rootPath"/>.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    public static string GetPlanPath(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return Path.GetFullPath(Path.Combine(rootPath, PlanRelativePath));
    }

    /// <summary>Returns the full path of the result seal artifact under <paramref name="rootPath"/>.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    public static string GetResultSealPath(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return Path.GetFullPath(Path.Combine(rootPath, ResultSealRelativePath));
    }

    /// <summary>Reads the GitHub snapshot against a clean checkout of the current default branch, writes the plan artifact, and returns the plan.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="runId"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The checkout does not match the current default branch head, or the default branch changed while planning.</exception>
    public async Task<RoadmapAutomationPlan> CreatePlan(
        long runId,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        ValidateExpectedRepository(expectedRepository);
        var baseSha = await RequireCleanHead(cancellationToken).ConfigureAwait(false);
        var inputSnapshot = RoadmapWriteInputSnapshot.Capture(_rootPath);
        var project = RoadmapProject.Load(_rootPath);
        var reconciliation = GitHubRoadmapReconciliation.Load(_rootPath);
        ValidateRepository(project, reconciliation, expectedRepository);
        var snapshot = await ReadSnapshot(project, reconciliation, cancellationToken).ConfigureAwait(false);
        var defaultBranchOid = RequireDefaultBranchOid(snapshot);
        if (!string.Equals(baseSha, defaultBranchOid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation requires the checkout to match the current main branch OID.");
        }

        var requiredTransitions = GitHubRoadmapReconciler.GetRequiredClosedTransitions(
            project,
            reconciliation,
            snapshot.Issues);
        var snapshotDigest = ComputeSnapshotDigest(project, reconciliation, snapshot);
        var snapshotChanged = !string.Equals(snapshotDigest, reconciliation.SnapshotDigest, StringComparison.Ordinal)
            || !string.Equals(defaultBranchOid, reconciliation.RepositoryCommit, StringComparison.Ordinal);
        inputSnapshot.Verify();
        var finalHead = await RequireCleanHead(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(baseSha, finalHead, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation main base changed while planning.");
        }

        var manifestPath = Path.GetRelativePath(_rootPath, reconciliation.ManifestPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var plan = RoadmapAutomationPlan.Create(
            expectedRepository,
            runId,
            baseSha,
            defaultBranchOid,
            snapshotDigest,
            inputSnapshot.Digest,
            manifestPath,
            reconciliation.GetClosedTransitions(),
            requiredTransitions,
            snapshotChanged);
        plan.Write(GetPlanPath(_rootPath));
        return plan;
    }

    /// <summary>Throws unless the stored plan matches the expected digest and repository and is still current for a clean checkout.</summary>
    public async Task ValidatePlan(
        string expectedPlanDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        _ = await CreateValidation(
            expectedPlanDigest,
            expectedRepository,
            requireCleanCheckout: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates the stored plan, then adds its required closed transitions to the reconciliation manifest.</summary>
    public async Task ApplyPlan(
        string expectedPlanDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreateValidation(
            expectedPlanDigest,
            expectedRepository,
            requireCleanCheckout: true,
            cancellationToken).ConfigureAwait(false);
        if (validation.Plan.RequiredClosedTransitions.Count == 0)
        {
            return;
        }

        var manifestContent = validation.Reconciliation.SourceContent;
        var updatedContent = RoadmapClosedTransitionWriter.AddToManifest(
            manifestContent,
            validation.Plan.RequiredClosedTransitions);
        validation.InputSnapshot.Verify();
        RollbackFileWriteBatch.Apply(
            _rootPath,
            [new AtomicFileWrite(validation.Reconciliation.ManifestPath, updatedContent, manifestContent)],
            validation.InputSnapshot.Verify);
    }

    /// <summary>Validates the applied result, then writes and returns the seal binding it to the plan and the expected artifact digest.</summary>
    public async Task<RoadmapAutomationResultSeal> SealResult(
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreatePostApplyValidation(
            expectedPlanDigest,
            expectedRepository,
            cancellationToken).ConfigureAwait(false);
        var seal = RoadmapAutomationResultSeal.Create(
            validation.Plan,
            validation.BaseSha,
            expectedArtifactDigest,
            validation.Changes.Digest,
            validation.Changes.Paths.Count,
            validation.Changes.HasChanges);
        seal.Write(GetResultSealPath(_rootPath));
        return seal;
    }

    /// <summary>Throws unless the stored seal and the current working tree still match the expected plan, artifact, and result digests.</summary>
    public async Task VerifyResult(
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreatePostApplyValidation(
            expectedPlanDigest,
            expectedRepository,
            cancellationToken).ConfigureAwait(false);
        _ = ValidateResultSeal(
            validation,
            expectedPlanDigest,
            expectedArtifactDigest,
            expectedResultDigest);
    }

    /// <summary>
    /// Validates the sealed result as <see cref="PublishResult"/> does and describes the pull request it would create.
    /// Reads the live GitHub snapshot but changes nothing in GitHub or the checkout.
    /// </summary>
    public async Task<RoadmapGitHubPublicationPreview> PreviewResult(
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreatePublicationValidation(
            expectedPlanDigest,
            expectedArtifactDigest,
            expectedResultDigest,
            expectedRepository,
            cancellationToken).ConfigureAwait(false);
        return await RoadmapGitHubPublisher
            .Preview(_rootPath, validation.Plan, validation.BaseSha, validation.Changes, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Validates the sealed result and publishes its changes to GitHub as a pull request.</summary>
    /// <exception cref="InvalidOperationException">No GitHub client can be created.</exception>
    public async Task<RoadmapGitHubPublication> PublishResult(
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreatePublicationValidation(
            expectedPlanDigest,
            expectedArtifactDigest,
            expectedResultDigest,
            expectedRepository,
            cancellationToken).ConfigureAwait(false);
        using var ownedHttpClient = _httpClient is null
            ? GitHubHttpClient.CreateFromGitHubToken("roadmap publication")
            : null;
        var httpClient = _httpClient ?? ownedHttpClient
            ?? throw new InvalidOperationException("Roadmap automation could not create a GitHub client.");
        return await new RoadmapGitHubPublisher(_rootPath, httpClient)
            .Publish(validation.Plan, validation.BaseSha, validation.Changes, validation.PullRequestBody!, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PostApplyValidation> CreatePublicationValidation(
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        var validation = await CreatePostApplyValidation(
            expectedPlanDigest,
            expectedRepository,
            cancellationToken).ConfigureAwait(false);
        var seal = ValidateResultSeal(
            validation,
            expectedPlanDigest,
            expectedArtifactDigest,
            expectedResultDigest);
        if (!seal.HasChanges)
        {
            throw new InvalidOperationException("Roadmap publication requires a nonempty sealed result.");
        }

        return validation.PullRequestBody is null
            ? throw new InvalidOperationException("roadmap/config.json must define automation.pullRequestBody before roadmap publication.")
            : validation;
    }

    private async Task<PlanValidation> CreateValidation(
        string expectedPlanDigest,
        string expectedRepository,
        bool requireCleanCheckout,
        CancellationToken cancellationToken)
    {
        ValidateExpectedRepository(expectedRepository);
        var plan = RoadmapAutomationPlan.Load(GetPlanPath(_rootPath), expectedPlanDigest);
        var currentHead = requireCleanCheckout
            ? await RequireCleanHead(cancellationToken).ConfigureAwait(false)
            : await _gitRepository.ReadHead(_rootPath, cancellationToken).ConfigureAwait(false);
        var inputSnapshot = RoadmapWriteInputSnapshot.Capture(_rootPath);
        var project = RoadmapProject.Load(_rootPath);
        var reconciliation = GitHubRoadmapReconciliation.Load(_rootPath);
        ValidateRepository(project, reconciliation, expectedRepository);
        var snapshot = await ReadSnapshot(project, reconciliation, cancellationToken).ConfigureAwait(false);
        var defaultBranchOid = RequireDefaultBranchOid(snapshot);
        var snapshotDigest = ComputeSnapshotDigest(project, reconciliation, snapshot);
        var requiredTransitions = GitHubRoadmapReconciler.GetRequiredClosedTransitions(
            project,
            reconciliation,
            snapshot.Issues);
        inputSnapshot.Verify();
        RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            expectedPlanDigest,
            expectedRepository,
            currentHead,
            defaultBranchOid,
            snapshotDigest,
            inputSnapshot.Digest,
            reconciliation.GetClosedTransitions(),
            requiredTransitions);
        return new PlanValidation(plan, reconciliation, inputSnapshot);
    }

    private async Task<PostApplyValidation> CreatePostApplyValidation(
        string expectedPlanDigest,
        string expectedRepository,
        CancellationToken cancellationToken)
    {
        ValidateExpectedRepository(expectedRepository);
        var plan = RoadmapAutomationPlan.Load(GetPlanPath(_rootPath), expectedPlanDigest);
        var currentHead = await _gitRepository.ReadHead(_rootPath, cancellationToken).ConfigureAwait(false);
        var inputSnapshot = RoadmapWriteInputSnapshot.Capture(_rootPath);
        var project = RoadmapProject.Load(_rootPath);
        var reconciliation = GitHubRoadmapReconciliation.Load(_rootPath);
        ValidateRepository(project, reconciliation, expectedRepository);
        var snapshot = await ReadSnapshot(project, reconciliation, cancellationToken).ConfigureAwait(false);
        var defaultBranchOid = RequireDefaultBranchOid(snapshot);
        var snapshotDigest = ComputeSnapshotDigest(project, reconciliation, snapshot);
        var requiredTransitions = GitHubRoadmapReconciler.GetRequiredClosedTransitions(
            project,
            reconciliation,
            snapshot.Issues);
        inputSnapshot.Verify();
        RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            expectedPlanDigest,
            expectedRepository,
            currentHead,
            defaultBranchOid,
            snapshotDigest,
            reconciliation.GetClosedTransitions(),
            requiredTransitions);
        if (!string.Equals(reconciliation.RepositoryCommit, defaultBranchOid, StringComparison.Ordinal)
            || !string.Equals(reconciliation.SnapshotDigest, plan.SnapshotDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation reconciliation manifest does not match the approved snapshot.");
        }

        var status = await _gitRepository.ReadStatus(_rootPath, cancellationToken).ConfigureAwait(false);
        var changes = RoadmapGitChangeGuard.Capture(_rootPath, status);
        return new PostApplyValidation(plan, currentHead, changes, project.AutomationPullRequestBody);
    }

    private async Task<string> RequireCleanHead(CancellationToken cancellationToken)
    {
        var head = await _gitRepository.ReadHead(_rootPath, cancellationToken).ConfigureAwait(false);
        var status = await _gitRepository.ReadStatus(_rootPath, cancellationToken).ConfigureAwait(false);
        var changes = RoadmapGitChangeGuard.Capture(_rootPath, status);
        if (changes.HasChanges)
        {
            throw new InvalidOperationException("Roadmap automation requires a clean main checkout before planning or applying approvals.");
        }

        return head;
    }

    private async Task<GitHubRoadmapReconcileSnapshot> ReadSnapshot(
        RoadmapProject project,
        GitHubRoadmapReconciliation reconciliation,
        CancellationToken cancellationToken)
    {
        using var ownedHttpClient = _httpClient is null
            ? GitHubHttpClient.CreateFromGitHubToken("roadmap automation")
            : null;
        var httpClient = _httpClient ?? ownedHttpClient
            ?? throw new InvalidOperationException("Roadmap automation could not create a GitHub client.");
        return await new GitHubRoadmapReconcileClient(httpClient)
            .ReadSnapshot(
                reconciliation.Repository,
                reconciliation.GetRequiredIssueNumbers(project),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ComputeSnapshotDigest(
        RoadmapProject project,
        GitHubRoadmapReconciliation reconciliation,
        GitHubRoadmapReconcileSnapshot snapshot) =>
        GitHubIssueSnapshotDigest.ComputeForOpenAndRequiredIssues(
            snapshot.Issues,
            reconciliation.GetRequiredIssueNumbers(project),
            reconciliation.Repository);

    private RoadmapAutomationResultSeal ValidateResultSeal(
        PostApplyValidation validation,
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest)
    {
        var seal = RoadmapAutomationResultSeal.Load(
            GetResultSealPath(_rootPath),
            expectedPlanDigest,
            expectedArtifactDigest,
            expectedResultDigest);
        if (!string.Equals(seal.BaseSha, validation.BaseSha, StringComparison.Ordinal)
            || !string.Equals(seal.SnapshotDigest, validation.Plan.SnapshotDigest, StringComparison.Ordinal)
            || seal.HasChanges != validation.Changes.HasChanges
            || seal.PathCount != validation.Changes.Paths.Count
            || !string.Equals(seal.ResultDigest, validation.Changes.Digest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation result changed after validation.");
        }

        return seal;
    }

    private static string RequireDefaultBranchOid(GitHubRoadmapReconcileSnapshot snapshot)
    {
        var oid = snapshot.RepositoryCommit;
        if (oid is null
            || oid.Length != 40
            || !oid.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidOperationException("Roadmap automation could not verify the default-branch OID.");
        }

        return oid;
    }

    private static void ValidateExpectedRepository(string repository)
    {
        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new InvalidOperationException("Roadmap automation requires GITHUB_REPOSITORY as owner/repository.");
        }
    }

    private static void ValidateRepository(
        RoadmapProject project,
        GitHubRoadmapReconciliation reconciliation,
        string expectedRepository)
    {
        if (!project.GitHubEnabled
            || !string.Equals(project.GitHubRepository, expectedRepository, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(reconciliation.Repository, expectedRepository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Roadmap automation repository inputs do not match GITHUB_REPOSITORY.");
        }
    }

    private sealed record PlanValidation(
        RoadmapAutomationPlan Plan,
        GitHubRoadmapReconciliation Reconciliation,
        RoadmapWriteInputSnapshot InputSnapshot);

    private sealed record PostApplyValidation(
        RoadmapAutomationPlan Plan,
        string BaseSha,
        RoadmapGitChangeSet Changes,
        string? PullRequestBody);
}
