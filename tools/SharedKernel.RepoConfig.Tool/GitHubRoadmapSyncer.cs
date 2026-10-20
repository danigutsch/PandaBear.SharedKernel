using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class GitHubRoadmapSyncer
{
    private const decimal ProjectNumberTolerance = 0.000001m;
    private static readonly (string Name, string DataType)[] RequiredProjectFields =
    [
        ("Roadmap order", "NUMBER"),
        ("RICE reach", "NUMBER"),
        ("RICE impact", "NUMBER"),
        ("RICE confidence", "NUMBER"),
        ("RICE effort", "NUMBER"),
        ("RICE score", "NUMBER"),
        ("Roadmap status", "SINGLE_SELECT"),
        ("Roadmap parent", "TEXT"),
        ("Roadmap blocked by", "TEXT"),
        ("Roadmap tags", "TEXT")
    ];
    private readonly HttpClient? _httpClient;
    private readonly RoadmapProject _project;
    private readonly TimeProvider _timeProvider;

    public GitHubRoadmapSyncer(RoadmapProject project)
        : this(project, httpClient: null, timeProvider: null)
    {
    }

    internal GitHubRoadmapSyncer(RoadmapProject project, HttpClient? httpClient, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        _project = project;
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GitHubSyncResult> Preview(CancellationToken cancellationToken)
    {
        var itemsWithIssues = GetItemsToSync();
        var preview = BuildPreview(itemsWithIssues);
        if (_project.GitHubProjectTarget is null)
        {
            return preview;
        }

        var repository = GetGitHubRepository();
        List<string> messages = [.. preview.Messages];
        using var ownedHttpClient = _httpClient is null ? GitHubHttpClient.Create("sync") : null;
        var httpClient = _httpClient ?? ownedHttpClient;
        var client = httpClient ?? throw new InvalidOperationException("GitHub sync could not create an HTTP client.");
        var projectClient = new GitHubProjectClient(client);
        await VerifyProjectTarget(projectClient, cancellationToken).ConfigureAwait(false);
        var target = _project.GitHubProjectTarget ?? throw new InvalidOperationException("GitHub Project target is required.");
        var fields = await RunProjectOperation(token => projectClient.GetFields(target, token), cancellationToken).ConfigureAwait(false);
        foreach (var conflict in ValidateProjectSchema(fields))
        {
            messages.Add($"dry-run: GitHub Project {target.Number} field {conflict} cannot be projected from roadmap source");
        }

        foreach (var item in itemsWithIssues.Where(item => item.GitHubIssue is not null))
        {
            await PreviewProjectItem(projectClient, repository, item, fields, messages, cancellationToken).ConfigureAwait(false);
        }

        return new GitHubSyncResult(messages);
    }

    public async Task<GitHubSyncResult> Apply(CancellationToken cancellationToken)
    {
        var repository = GetGitHubRepository();
        var itemsWithIssues = GetItemsToSync();
        if (itemsWithIssues.Length == 0)
        {
            return EmptySyncResult();
        }

        List<string> messages = [];
        using var ownedHttpClient = _httpClient is null ? GitHubHttpClient.Create("sync") : null;
        var httpClient = _httpClient ?? ownedHttpClient;
        var client = httpClient ?? throw new InvalidOperationException("GitHub sync could not create an HTTP client.");
        var projectClient = CreateProjectClient(client);
        await VerifyProjectTarget(projectClient, cancellationToken).ConfigureAwait(false);

        foreach (var configuredItem in itemsWithIssues)
        {
            await SyncItem(client, repository, projectClient, configuredItem, messages, cancellationToken).ConfigureAwait(false);
        }

        return new GitHubSyncResult(messages);
    }

    private string GetGitHubRepository()
    {
        if (!_project.GitHubEnabled)
        {
            throw new InvalidOperationException("GitHub sync is disabled in roadmap/config.json.");
        }

        return string.IsNullOrWhiteSpace(_project.GitHubRepository)
            ? throw new InvalidOperationException("roadmap/config.json must define integrations.github.repository before GitHub sync.")
            : _project.GitHubRepository;
    }

    private RoadmapItemSnapshot[] GetItemsToSync() =>
        _project.Items
            .Where(item => item.GitHubIssue is not null || item.CreateGitHubIssue)
            .OrderBy(item => item.IsTriaged ? 0 : 1)
            .ThenBy(item => item.Order ?? int.MaxValue)
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

    private static GitHubSyncResult EmptySyncResult() =>
        new(["No roadmap items have GitHub issue mappings or explicit creation requests."]);

    private GitHubProjectClient? CreateProjectClient(HttpClient httpClient) =>
        _project.GitHubProjectTarget is null ? null : new GitHubProjectClient(httpClient);

    private async Task VerifyProjectTarget(GitHubProjectClient? projectClient, CancellationToken cancellationToken)
    {
        if (projectClient is null)
        {
            return;
        }

        var target = _project.GitHubProjectTarget ?? throw new InvalidOperationException("GitHub Project target is required.");
        await RunProjectOperation(async token =>
        {
            await projectClient.VerifyTarget(target, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncItem(
        HttpClient httpClient,
        string repository,
        GitHubProjectClient? projectClient,
        RoadmapItemSnapshot configuredItem,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        var item = await CreateOrAdoptIssue(httpClient, repository, configuredItem, messages, cancellationToken).ConfigureAwait(false);
        await SyncLabels(httpClient, repository, item, messages, cancellationToken).ConfigureAwait(false);
        await ProjectItem(projectClient, repository, item, messages, cancellationToken).ConfigureAwait(false);

        if (item.Labels.Count == 0 && projectClient is null)
        {
            messages.Add($"skipped {repository}#{item.GitHubIssue} from {item.Id} because it has no labels");
        }
    }

    private async Task<RoadmapItemSnapshot> CreateOrAdoptIssue(
        HttpClient httpClient,
        string repository,
        RoadmapItemSnapshot item,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (!item.CreateGitHubIssue)
        {
            return item;
        }

        var creationLock = AcquireIssueCreationLock(item);
        var retainCreationLock = false;
        var creationRequestStarted = false;
        var issueCreated = false;
        try
        {
            var mapping = PrepareIssueMapping(item);
            if (TryGetMappedIssueNumber(mapping.GitHub, out var existingIssueNumber))
            {
                return item with { GitHubIssue = existingIssueNumber, CreateGitHubIssue = false };
            }

            EnsureCreateIntent(mapping.GitHub, item.Path);
            creationRequestStarted = true;
            var creation = await RunProjectOperation(token => CreateIssue(httpClient, repository, item, token), cancellationToken).ConfigureAwait(false);
            if (creation.FailureStatus is HttpStatusCode failureStatus)
            {
                retainCreationLock = (int)failureStatus >= 500;
                throw CreateGitHubFailure("issue creation", failureStatus);
            }

            var issueNumber = creation.IssueNumber ?? throw new JsonException("GitHub issue creation response did not contain a positive issue number.");
            issueCreated = true;
            PersistCreatedIssueMapping(item, issueNumber);
            messages.Add($"created {repository}#{issueNumber} from {item.Id}");
            return item with { GitHubIssue = issueNumber, CreateGitHubIssue = false };
        }
        catch (Exception exception) when (issueCreated || (creationRequestStarted && IsAmbiguousIssueCreationFailure(exception)))
        {
            retainCreationLock = true;
            throw;
        }
        finally
        {
            var lockPath = creationLock.Name;
            await creationLock.DisposeAsync().ConfigureAwait(false);
            if (!retainCreationLock)
            {
                File.Delete(lockPath);
            }
        }
    }

    private void PersistCreatedIssueMapping(RoadmapItemSnapshot item, int issueNumber)
    {
        var mapping = PrepareIssueMapping(item);
        try
        {
            EnsureCreateIntent(mapping.GitHub, item.Path);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"GitHub issue #{issueNumber} was created but its roadmap mapping changed while synchronizing: {item.Path}. Verify the mapping before retrying.", exception);
        }

        try
        {
            PersistIssueMapping(mapping.Path, mapping.Root, issueNumber);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"GitHub issue #{issueNumber} was created but its roadmap mapping could not be persisted. Set integrations.github.issue to {issueNumber} in {item.Path} before retrying.", exception);
        }
    }

    private static bool IsAmbiguousIssueCreationFailure(Exception exception)
    {
        return exception is TimeoutException or OperationCanceledException or HttpRequestException or IOException or JsonException;
    }

    private async Task SyncLabels(
        HttpClient httpClient,
        string repository,
        RoadmapItemSnapshot item,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        var extraLabels = await UpdateItem(httpClient, repository, item, cancellationToken).ConfigureAwait(false);
        if (item.Labels.Count > 0)
        {
            messages.Add($"updated labels for {repository}#{item.GitHubIssue} from {item.Id}");
        }

        foreach (var label in extraLabels)
        {
            messages.Add($"drift: {repository}#{item.GitHubIssue} has extra GitHub label {label}");
        }
    }

    private async Task ProjectItem(
        GitHubProjectClient? projectClient,
        string repository,
        RoadmapItemSnapshot item,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (projectClient is null)
        {
            return;
        }

        var target = _project.GitHubProjectTarget ?? throw new InvalidOperationException("GitHub Project target is required.");
        var issueNumber = item.GitHubIssue ?? throw new InvalidOperationException("GitHub issue mapping is required.");
        var projection = await RunProjectOperation(token => AssessProjectProjection(projectClient, target, repository, item, appliesChanges: true, token), cancellationToken).ConfigureAwait(false);
        messages.Add(projection.IsBlocked
            ? $"skipped projection of {repository}#{issueNumber} to GitHub Project {target.Number} because Roadmap status cannot be projected from roadmap source"
            : $"projected {repository}#{issueNumber} to GitHub Project {target.Number}");
        foreach (var conflict in projection.Conflicts)
        {
            messages.Add($"drift: {repository}#{issueNumber} Project field {conflict} cannot be projected from roadmap source");
        }
    }

    private async Task PreviewProjectItem(
        GitHubProjectClient projectClient,
        string repository,
        RoadmapItemSnapshot item,
        IReadOnlyList<GitHubProjectResponse.ProjectField> fields,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        var target = _project.GitHubProjectTarget ?? throw new InvalidOperationException("GitHub Project target is required.");
        var issueNumber = item.GitHubIssue ?? throw new InvalidOperationException("GitHub issue mapping is required.");
        var projection = await RunProjectOperation(token => AssessProjectProjection(projectClient, target, repository, item, fields, appliesChanges: false, token), cancellationToken).ConfigureAwait(false);
        if (projection.IsBlocked)
        {
            messages.Add($"dry-run: skipped projection of {repository}#{issueNumber} to GitHub Project {target.Number} because Roadmap status cannot be projected from roadmap source");
        }
        else
        {
            messages.Add(projection.HasExistingMembership
                ? $"dry-run: GitHub Project {target.Number} already contains {repository}#{issueNumber}"
                : $"dry-run: add {repository}#{issueNumber} to GitHub Project {target.Number}");

            foreach (var change in projection.ProposedChanges)
            {
                messages.Add($"dry-run: set {repository}#{issueNumber} Project field {change}");
            }

            foreach (var change in projection.DeferredChanges)
            {
                messages.Add($"dry-run: set {repository}#{issueNumber} Project field {change} after adding it");
            }
        }

        foreach (var conflict in projection.Conflicts)
        {
            messages.Add($"drift: {repository}#{issueNumber} Project field {conflict} cannot be projected from roadmap source");
        }
    }

    private async Task<ProjectProjectionResult> AssessProjectProjection(
        GitHubProjectClient projectClient,
        GitHubProjectTarget target,
        string repository,
        RoadmapItemSnapshot item,
        bool appliesChanges,
        CancellationToken cancellationToken)
    {
        var fields = await projectClient.GetFields(target, cancellationToken).ConfigureAwait(false);
        return await AssessProjectProjection(projectClient, target, repository, item, fields, appliesChanges, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProjectProjectionResult> AssessProjectProjection(
        GitHubProjectClient projectClient,
        GitHubProjectTarget target,
        string repository,
        RoadmapItemSnapshot item,
        IReadOnlyList<GitHubProjectResponse.ProjectField> fields,
        bool appliesChanges,
        CancellationToken cancellationToken)
    {
        if (HasInvalidRequiredProjectStatusOptions(fields))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invalidSchemaResult = new ProjectProjectionResult(hasExistingMembership: false, appliesChanges: appliesChanges, valuesAreKnown: false)
            {
                IsBlocked = true
            };
            invalidSchemaResult.Conflicts.Add("Roadmap status");
            return invalidSchemaResult;
        }

        var issueNumber = item.GitHubIssue ?? throw new InvalidOperationException("GitHub issue mapping is required.");
        var issueId = await projectClient.GetIssueNodeId(repository, issueNumber, cancellationToken).ConfigureAwait(false);
        var projectItemId = await projectClient.FindItemId(target, issueId, cancellationToken).ConfigureAwait(false);
        var result = new ProjectProjectionResult(projectItemId is not null, appliesChanges, projectItemId is not null);
        if (projectItemId is null && appliesChanges)
        {
            projectItemId = await projectClient.AddIssue(target, issueId, cancellationToken).ConfigureAwait(false);
            result.ValuesAreKnown = true;
        }

        var values = projectItemId is null
            ? Array.Empty<GitHubProjectItemResponse.FieldValue>()
            : await projectClient.GetFieldValues(projectItemId, cancellationToken).ConfigureAwait(false);
        var projection = new ProjectFieldProjection(projectClient, target, projectItemId ?? string.Empty, fields, values, result, cancellationToken);
        await PopulateProjectFields(projection, item).ConfigureAwait(false);
        return result;
    }

    private string[] ValidateProjectSchema(IReadOnlyList<GitHubProjectResponse.ProjectField> fields)
    {
        List<string> conflicts = [];
        foreach (var (name, dataType) in RequiredProjectFields)
        {
            var candidates = fields.Where(field => string.Equals(field.Name, name, StringComparison.Ordinal)).ToArray();
            if (candidates.Length != 1
                || string.IsNullOrWhiteSpace(candidates[0].Id)
                || !string.Equals(candidates[0].DataType, dataType, StringComparison.Ordinal))
            {
                conflicts.Add(name);
            }
        }

        if (HasInvalidRequiredProjectStatusOptions(fields))
        {
            conflicts.Add("Roadmap status");
        }

        return conflicts.Distinct(StringComparer.Ordinal).ToArray();
    }

    private bool HasInvalidRequiredProjectStatusOptions(IReadOnlyList<GitHubProjectResponse.ProjectField> fields)
    {
        var statusFields = fields.Where(field => string.Equals(field.Name, "Roadmap status", StringComparison.Ordinal)).ToArray();
        if (statusFields.Length != 1)
        {
            return true;
        }

        var statusField = statusFields[0];
        var statusOptions = statusField.Options;
        if (string.IsNullOrWhiteSpace(statusField.Id)
            || !string.Equals(statusField.DataType, "SINGLE_SELECT", StringComparison.Ordinal))
        {
            return true;
        }

        if (statusOptions is null)
        {
            return true;
        }

        foreach (var status in _project.AllowedStatuses)
        {
            var matchingOptions = statusOptions.Where(option => string.Equals(option.Name, status, StringComparison.Ordinal)).ToArray();
            if (matchingOptions.Length != 1
                || string.IsNullOrWhiteSpace(matchingOptions[0].Id)
                || statusOptions.Count(option => string.Equals(option.Id, matchingOptions[0].Id, StringComparison.Ordinal)) != 1)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> UpdateItem(HttpClient httpClient, string repository, RoadmapItemSnapshot item, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration, _timeProvider);
        using var itemCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await UpdateIssue(httpClient, repository, item, itemCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("sync");
        }
    }

    private async Task<T> RunProjectOperation<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration, _timeProvider);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await operation(operationCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("sync");
        }
    }

    private static async Task PopulateProjectFields(ProjectFieldProjection projection, RoadmapItemSnapshot item)
    {
        if (item.IsTriaged)
        {
            var order = item.Order ?? throw new InvalidOperationException("Triaged roadmap items require an order.");
            var reach = item.Reach ?? throw new InvalidOperationException("Triaged roadmap items require RICE reach.");
            var impact = item.Impact ?? throw new InvalidOperationException("Triaged roadmap items require RICE impact.");
            var confidence = item.Confidence ?? throw new InvalidOperationException("Triaged roadmap items require RICE confidence.");
            var effort = item.Effort ?? throw new InvalidOperationException("Triaged roadmap items require RICE effort.");
            var score = item.Score ?? throw new InvalidOperationException("Triaged roadmap items require a RICE score.");
            await projection.ProjectNumber("Roadmap order", order).ConfigureAwait(false);
            await projection.ProjectNumber("RICE reach", reach).ConfigureAwait(false);
            await projection.ProjectNumber("RICE impact", impact).ConfigureAwait(false);
            await projection.ProjectNumber("RICE confidence", confidence).ConfigureAwait(false);
            await projection.ProjectNumber("RICE effort", effort).ConfigureAwait(false);
            await projection.ProjectNumber("RICE score", score).ConfigureAwait(false);
        }

        await projection.ProjectStatus(item.Status).ConfigureAwait(false);
        await projection.ProjectText("Roadmap parent", item.Parent ?? string.Empty).ConfigureAwait(false);
        await projection.ProjectText("Roadmap blocked by", string.Join(", ", item.BlockedBy)).ConfigureAwait(false);
        await projection.ProjectText("Roadmap tags", string.Join(", ", item.Tags)).ConfigureAwait(false);

    }

    private sealed class ProjectProjectionResult(bool hasExistingMembership, bool appliesChanges, bool valuesAreKnown)
    {
        public bool HasExistingMembership { get; } = hasExistingMembership;

        public bool AppliesChanges { get; } = appliesChanges;

        public bool ValuesAreKnown { get; set; } = valuesAreKnown;

        public bool IsBlocked { get; set; }

        public List<string> Conflicts { get; } = [];

        public List<string> ProposedChanges { get; } = [];

        public List<string> DeferredChanges { get; } = [];
    }

    private sealed class ProjectFieldProjection(
        GitHubProjectClient projectClient,
        GitHubProjectTarget target,
        string itemId,
        IReadOnlyList<GitHubProjectResponse.ProjectField> fields,
        IReadOnlyList<GitHubProjectItemResponse.FieldValue> values,
        ProjectProjectionResult result,
        CancellationToken cancellationToken)
    {
        public async Task ProjectNumber(string fieldName, decimal value)
        {
            var field = FindCompatibleField(fieldName, "NUMBER");
            if (field is null)
            {
                return;
            }

            var fieldId = field.Id ?? throw new InvalidOperationException("GitHub Project field identifier is required.");
            if (!result.ValuesAreKnown)
            {
                result.DeferredChanges.Add($"{fieldName} to {value.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            var existing = values.FirstOrDefault(candidate => string.Equals(candidate.Field?.Id, field.Id, StringComparison.Ordinal));
            if (existing?.Number is decimal existingNumber && Math.Abs(existingNumber - value) > ProjectNumberTolerance)
            {
                result.Conflicts.Add(fieldName);
                return;
            }

            if (existing?.Number is null)
            {
                if (result.AppliesChanges)
                {
                    await projectClient.UpdateNumber(target, itemId, fieldId, value, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result.ProposedChanges.Add($"{fieldName} to {value.ToString(CultureInfo.InvariantCulture)}");
                }
            }
        }

        public async Task ProjectStatus(string status)
        {
            var field = FindCompatibleField("Roadmap status", "SINGLE_SELECT");
            if (field is null)
            {
                return;
            }

            var fieldId = field.Id ?? throw new InvalidOperationException("GitHub Project field identifier is required.");
            var matchingOptions = field.Options?
                .Where(candidate => string.Equals(candidate.Name, status, StringComparison.Ordinal))
                .ToArray()
                ?? [];
            var optionId = matchingOptions.Length == 1 ? matchingOptions[0].Id : null;
            var matchingOptionIdCount = field.Options?.Count(candidate => string.Equals(candidate.Id, optionId, StringComparison.Ordinal)) ?? 0;
            if (matchingOptions.Length != 1
                || string.IsNullOrWhiteSpace(optionId)
                || matchingOptionIdCount != 1)
            {
                result.Conflicts.Add("Roadmap status");
                return;
            }

            if (!result.ValuesAreKnown)
            {
                result.DeferredChanges.Add($"Roadmap status to {status}");
                return;
            }

            var existing = values.FirstOrDefault(candidate => string.Equals(candidate.Field?.Id, field.Id, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(existing?.OptionId) && !string.Equals(existing.OptionId, optionId, StringComparison.Ordinal))
            {
                result.Conflicts.Add("Roadmap status");
                return;
            }

            if (string.IsNullOrWhiteSpace(existing?.OptionId))
            {
                if (result.AppliesChanges)
                {
                    await projectClient.UpdateSingleSelect(target, itemId, fieldId, optionId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result.ProposedChanges.Add($"Roadmap status to {status}");
                }
            }
        }

        public async Task ProjectText(string fieldName, string value)
        {
            var field = FindCompatibleField(fieldName, "TEXT");
            if (field is null)
            {
                return;
            }

            var fieldId = field.Id ?? throw new InvalidOperationException("GitHub Project field identifier is required.");
            if (!result.ValuesAreKnown)
            {
                result.DeferredChanges.Add($"{fieldName} to {value}");
                return;
            }

            var existing = values.FirstOrDefault(candidate => string.Equals(candidate.Field?.Id, field.Id, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(existing?.Text) && !string.Equals(existing.Text, value, StringComparison.Ordinal))
            {
                result.Conflicts.Add(fieldName);
                return;
            }

            if (string.IsNullOrWhiteSpace(existing?.Text))
            {
                if (result.AppliesChanges)
                {
                    await projectClient.UpdateText(target, itemId, fieldId, value, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result.ProposedChanges.Add($"{fieldName} to {value}");
                }
            }
        }

        private GitHubProjectResponse.ProjectField? FindCompatibleField(string fieldName, string dataType)
        {
            var candidates = fields.Where(field => string.Equals(field.Name, fieldName, StringComparison.Ordinal)).ToArray();
            if (candidates.Length != 1
                || string.IsNullOrWhiteSpace(candidates[0].Id)
                || !string.Equals(candidates[0].DataType, dataType, StringComparison.Ordinal))
            {
                result.Conflicts.Add(fieldName);
                return null;
            }

            return candidates[0];
        }
    }

    private GitHubSyncResult BuildPreview(RoadmapItemSnapshot[] itemsWithIssues)
    {
        if (!_project.GitHubEnabled)
        {
            throw new InvalidOperationException("GitHub sync is disabled in roadmap/config.json.");
        }

        if (string.IsNullOrWhiteSpace(_project.GitHubRepository))
        {
            throw new InvalidOperationException("roadmap/config.json must define integrations.github.repository before GitHub sync.");
        }

        if (itemsWithIssues.Length == 0)
        {
            return EmptySyncResult();
        }

        return new GitHubSyncResult(itemsWithIssues
            .SelectMany(item => BuildPreviewMessages(item))
            .ToArray());
    }

    private IEnumerable<string> BuildPreviewMessages(RoadmapItemSnapshot item)
    {
        if (item.CreateGitHubIssue)
        {
            yield return $"dry-run: create GitHub issue for {_project.GitHubRepository} from {item.Id}";
            if (_project.GitHubProjectTarget is not null)
            {
                yield return $"dry-run: ensure the created issue from {item.Id} is in GitHub Project {_project.GitHubProjectTarget.Number}";
            }

            yield break;
        }

        if (item.Labels.Count > 0)
        {
            yield return $"dry-run: sync labels for {_project.GitHubRepository}#{item.GitHubIssue} from {item.Id}";
        }
        else if (_project.GitHubProjectTarget is null)
        {
            yield return $"dry-run: skip {_project.GitHubRepository}#{item.GitHubIssue} from {item.Id} because it has no labels";
        }

        if (_project.GitHubProjectTarget is not null)
        {
            yield return $"dry-run: ensure {_project.GitHubRepository}#{item.GitHubIssue} is in GitHub Project {_project.GitHubProjectTarget.Number}";
        }
    }

    private static async Task<IReadOnlyList<string>> UpdateIssue(HttpClient httpClient, string repository, RoadmapItemSnapshot item, CancellationToken cancellationToken)
    {
        var issueNumber = item.GitHubIssue ?? throw new InvalidOperationException("GitHub issue mapping is required.");
        var issueUrl = $"https://api.github.com/repos/{repository}/issues/{issueNumber}";
        await VerifyIssueMapping(httpClient, repository, issueNumber, cancellationToken).ConfigureAwait(false);
        var extraLabels = await GetExtraLabels(httpClient, issueUrl, item.Labels, issueNumber, cancellationToken).ConfigureAwait(false);

        if (item.Labels.Count > 0)
        {
            var labelPayload = BuildLabelPayload(item.Labels);
            using var labelRequest = new HttpRequestMessage(HttpMethod.Post, $"{issueUrl}/labels")
            {
                Content = new StringContent(labelPayload, Encoding.UTF8, "application/json")
            };
            using var labelResponse = await httpClient.SendAsync(labelRequest, cancellationToken).ConfigureAwait(false);
            if (!labelResponse.IsSuccessStatusCode)
            {
                throw CreateGitHubFailure("label sync", labelResponse.StatusCode, issueNumber);
            }
        }

        return extraLabels;
    }

    private static async Task<IReadOnlyList<string>> GetExtraLabels(HttpClient httpClient, string issueUrl, IReadOnlyList<string> labels, int issueNumber, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, issueUrl);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateGitHubFailure("label drift check", response.StatusCode, issueNumber);
        }

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("labels", out var remoteLabels) || remoteLabels.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return remoteLabels.EnumerateArray()
            .Where(label => label.ValueKind == JsonValueKind.Object && label.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            .Select(label => label.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name) && !labels.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Select(name => name ?? string.Empty)
            .ToArray();
    }

    private static async Task<(int? IssueNumber, HttpStatusCode? FailureStatus)> CreateIssue(HttpClient httpClient, string repository, RoadmapItemSnapshot item, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{repository}/issues")
        {
            Content = new StringContent(BuildIssuePayload(item), Encoding.UTF8, "application/json")
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (null, response.StatusCode);
        }

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("number", out var number) || !number.TryGetInt32(out var issueNumber) || issueNumber < 1)
        {
            throw new JsonException("GitHub issue creation response did not contain a positive issue number.");
        }

        return (issueNumber, null);
    }

    private (string Path, JsonObject Root, JsonObject GitHub) PrepareIssueMapping(RoadmapItemSnapshot item)
    {
        var itemPath = Path.Combine(_project.RootPath, item.Path);
        var root = JsonNode.Parse(File.ReadAllText(itemPath)) as JsonObject
            ?? throw new InvalidOperationException($"Roadmap item root must be a JSON object: {item.Path}.");
        var integrations = GetOrCreateObject(root, "integrations");
        var github = GetOrCreateObject(integrations, "github");
        return (itemPath, root, github);
    }

    private FileStream AcquireIssueCreationLock(RoadmapItemSnapshot item)
    {
        var lockPath = Path.Combine(_project.RootPath, item.Path) + ".lock";
        try
        {
            return new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"GitHub issue creation is already in progress for {item.Id}. If no sync is running, verify whether an issue was created, then remove stale lock file {lockPath} and retry.", exception);
        }
    }

    private static bool TryGetMappedIssueNumber(JsonObject github, out int issueNumber)
    {
        issueNumber = 0;
        return github["issue"] is JsonValue value && value.TryGetValue(out issueNumber) && issueNumber > 0;
    }

    private static void EnsureCreateIntent(JsonObject github, string itemPath)
    {
        if (github["issue"] is JsonValue value && value.TryGetValue<string>(out var intent) && string.Equals(intent, "create", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"GitHub issue mapping changed while synchronizing: {itemPath}.");
    }

    private static void PersistIssueMapping(string itemPath, JsonObject root, int issueNumber)
    {
        var integrations = GetOrCreateObject(root, "integrations");
        var github = GetOrCreateObject(integrations, "github");
        github["issue"] = issueNumber;

        var temporaryPath = itemPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            File.Move(temporaryPath, itemPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static async Task VerifyIssueMapping(HttpClient httpClient, string repository, int issueNumber, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/pulls/{issueNumber}");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub issue mapping points to a pull request: #{issueNumber}.");
        }

        throw CreateGitHubFailure("pull request check", response.StatusCode, issueNumber);
    }

    private static InvalidOperationException CreateGitHubFailure(string operation, HttpStatusCode statusCode, int? issueNumber = null)
    {
        var hint = statusCode switch
        {
            HttpStatusCode.Unauthorized => " (authentication required)",
            HttpStatusCode.Forbidden => " (access denied or rate limited)",
            HttpStatusCode.NotFound => " (resource not found or inaccessible)",
            HttpStatusCode.UnprocessableEntity => " (request validation failed)",
            HttpStatusCode.TooManyRequests => " (rate limited)",
            _ => string.Empty
        };

        var issueSuffix = issueNumber is null ? string.Empty : $" for #{issueNumber.Value}";
        return new InvalidOperationException($"GitHub {operation} failed{issueSuffix}: HTTP {(int)statusCode}{hint}.");
    }

    private static string BuildLabelPayload(IReadOnlyList<string> labels)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("labels");
            foreach (var label in labels)
            {
                writer.WriteStringValue(label);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string BuildIssuePayload(RoadmapItemSnapshot item)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("title", item.Title);
            writer.WriteStartArray("labels");
            foreach (var label in item.Labels)
            {
                writer.WriteStringValue(label);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

}
