using System.Globalization;
using SharedKernel.InputNormalization;

namespace SharedKernel.Repositories.GitHub;

/// <summary>Reads roadmap issue metadata from the GitHub GraphQL API.</summary>
/// <param name="httpClient">An authenticated GitHub client with issue read access.</param>
public sealed class GitHubForgeIssueReader(HttpClient httpClient) : IForgeIssueReader
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMinutes(2);
    private readonly GitHubRoadmapReconcileClient _client = new(httpClient);

    /// <summary>Creates an issue reader using the authenticated client's connection.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection has no token.</exception>
    /// <exception cref="ObjectDisposedException">The connection is disposed.</exception>
    public GitHubForgeIssueReader(GitHubConnection connection)
        : this((connection ?? throw new ArgumentNullException(nameof(connection))).Client)
    {
    }

    /// <inheritdoc/>
    public ForgeIssueCapabilities Capabilities => ForgeIssueCapabilities.StateReason | ForgeIssueCapabilities.Parent | ForgeIssueCapabilities.Blockers;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ForgeIssue>> ReadOpenIssues(string repository, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        var issues = await Read(
            token => _client.ReadOpenRepositoryIssues(repository, token),
            repository,
            cancellationToken).ConfigureAwait(false);
        return [.. issues.Select(issue => MapIssue(repository, issue))];
    }

    /// <inheritdoc/>
    public async Task<ForgeIssue> ReadIssue(ForgeIssueReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Repository);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reference.Number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reference.Number, int.MaxValue);
        var subject = string.Create(CultureInfo.InvariantCulture, $"{reference.Repository}#{reference.Number}");
        var issue = await Read(
            token => _client.ReadRepositoryIssue(reference.Repository, (int)reference.Number, token),
            subject,
            cancellationToken).ConfigureAwait(false);
        return MapIssue(reference.Repository, issue);
    }

    private static async Task<T> Read<T>(Func<CancellationToken, Task<T>> action, string subject, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(ReadTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await action(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException("GitHub issue read timed out for " + ControlCharacters.Escape(subject) + ".");
        }
        catch (GitHubIssueLookupUnavailableException exception)
        {
            throw new InvalidOperationException("GitHub could not read issue " + ControlCharacters.Escape(subject) + " because it was missing or inaccessible.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                "GitHub could not read " + ControlCharacters.Escape(subject) + ". " + exception.Message,
                exception);
        }
    }

    private static ForgeIssue MapIssue(string repository, GitHubRoadmapReconcileIssue issue) => new(
        new ForgeIssueReference(repository, issue.Number),
        issue.Title,
        string.Equals(issue.State, "OPEN", StringComparison.Ordinal),
        issue.StateReason,
        issue.Labels,
        issue.Parent is null ? null : MapLink(issue.Parent),
        [.. issue.BlockedBy.Select(MapLink)]);

    private static ForgeIssueLink MapLink(GitHubRoadmapReconcileRelation relation) => new(
        new ForgeIssueReference(relation.Repository, relation.Number),
        string.Equals(relation.State, "OPEN", StringComparison.Ordinal));
}
