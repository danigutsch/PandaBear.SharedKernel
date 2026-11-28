using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using SharedKernel.Forgejo;
using SharedKernel.IO;

namespace SharedKernel.Repositories.Forgejo;

/// <summary>Reads roadmap issue metadata through the Forgejo REST API.</summary>
public sealed class ForgejoIssueReader : IForgeIssueReader, IDisposable
{
    private const int MaxPages = 100;
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private const int MaxConcurrentBlockerReads = 4;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;

    /// <summary>Creates a reader for the instance's issues and their dependencies.</summary>
    /// <param name="handler">The HTTP handler, owned by the reader. Use a handler that refuses redirects.</param>
    /// <param name="server">The HTTPS address of the Forgejo instance.</param>
    /// <param name="userAgent">The calling application and version.</param>
    /// <param name="token">An issue read token or <see langword="null"/> for anonymous access.</param>
    public ForgejoIssueReader(HttpMessageHandler handler, Uri server, ProductInfoHeaderValue userAgent, string? token)
    {
        _client = ForgejoApiClient.Create(handler, server, userAgent, token);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc/>
    public ForgeIssueCapabilities Capabilities => ForgeIssueCapabilities.Blockers;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ForgeIssue>> ReadOpenIssues(string repository, CancellationToken cancellationToken)
    {
        var path = RepositoryIssuesPath(repository);
        return await ReadWithDeadline<IReadOnlyList<ForgeIssue>>(repository, async token =>
        {
            var entries = await ReadPages(path, "state=open&type=issues&", ReadIssueMetadata, token).ConfigureAwait(false);
            var numbers = new HashSet<long>();
            foreach (var entry in entries)
            {
                if (!entry.IsOpen || !SameRepository(entry.Reference.Repository, repository) || !numbers.Add(entry.Reference.Number))
                {
                    throw new InvalidOperationException("Forgejo reported inconsistent open issue metadata.");
                }
            }

            var issues = new ForgeIssue[entries.Count];
            await Parallel.ForAsync(0, issues.Length, new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = MaxConcurrentBlockerReads
            }, async (index, blockerCancellation) =>
            {
                var entry = entries[index];
                issues[index] = await ReadBlockers(path, entry, blockerCancellation).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return [.. issues.OrderBy(static issue => issue.Reference.Number)];
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ForgeIssue> ReadIssue(ForgeIssueReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reference.Number);
        var path = RepositoryIssuesPath(reference.Repository);
        return await ReadWithDeadline(Display(reference), async token =>
        {
            var entry = await Get(IssuePath(path, reference.Number), static (root, _) => ReadIssueMetadata(root), token).ConfigureAwait(false);
            if (entry.Reference.Number != reference.Number || !SameRepository(entry.Reference.Repository, reference.Repository))
            {
                throw new InvalidOperationException("Forgejo returned a different issue.");
            }

            return await ReadBlockers(path, entry, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the HTTP client and its handler.</summary>
    public void Dispose() => _client.Dispose();

    private async Task<ForgeIssue> ReadBlockers(string path, ForgeIssue issue, CancellationToken cancellationToken)
    {
        try
        {
            var links = await ReadPages(IssuePath(path, issue.Reference.Number) + "/dependencies", string.Empty, ReadLink, cancellationToken).ConfigureAwait(false);
            return issue with { BlockedBy = links };
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"Forgejo could not read blockers for {Display(issue.Reference)}. {exception.Message}", exception);
        }
    }

    private async Task<IReadOnlyList<T>> ReadPages<T>(string path, string query, Func<JsonElement, T> parse, CancellationToken cancellationToken)
    {
        long? advertisedTotal = null;
        var previousPageHadPagingHeaders = false;
        var items = await ForgejoPager.ReadAll((page, token) => Get(
            string.Create(CultureInfo.InvariantCulture, $"{path}?{query}limit={ForgejoPager.PageSize}&page={page}"),
            (root, headers) =>
            {
                var response = ForgejoPager.Page(root.EnumerateArray().Select(parse).ToArray(), headers);
                // The pager only requests another page after a paging header when the response promises more items.
                if (previousPageHadPagingHeaders && response.Items.Count == 0)
                {
                    throw new InvalidOperationException("Forgejo returned incomplete issue results after advertising another page.");
                }

                if (advertisedTotal is { } expected && response.TotalCount is { } reported && expected != reported)
                {
                    throw new InvalidOperationException("Forgejo returned incomplete issue results with inconsistent total counts.");
                }

                advertisedTotal ??= response.TotalCount;
                previousPageHadPagingHeaders = response.Link is not null || response.TotalCount is not null;
                return response;
            }, token), MaxPages, "Forgejo issue results", cancellationToken).ConfigureAwait(false);

        if (advertisedTotal is { } total && items.Count != total)
        {
            throw new InvalidOperationException("Forgejo returned incomplete issue results for the advertised total count.");
        }

        return items;
    }

    private async Task<T> Get<T>(string path, Func<JsonElement, HttpResponseHeaders, T> parse, CancellationToken cancellationToken)
    {
        var uri = new Uri(_client.BaseAddress!, path);
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Forgejo returned HTTP {(int)response.StatusCode}."));
        }

        if (response.Content.Headers.ContentLength > MaxResponseBytes)
        {
            throw new InvalidOperationException("Forgejo's issue response exceeded the allowed size.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] content;
        await using (stream.ConfigureAwait(false))
        {
            content = await BoundedReader.ReadToEnd(stream, MaxResponseBytes, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Forgejo's issue response exceeded the allowed size.");
        }

        using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowDuplicateProperties = false });
        return parse(document.RootElement, response.Headers);
    }

    private static async Task<T> ReadWithDeadline<T>(string subject, Func<CancellationToken, Task<T>> request, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(ReadTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await request(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Forgejo timed out reading {subject}.");
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or IOException or InvalidDataException)
        {
            throw new InvalidOperationException($"Forgejo could not read {subject}. The response was unavailable or incomplete.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"Forgejo could not read {subject}. {exception.Message}", exception);
        }
    }

    private static ForgeIssue ReadIssueMetadata(JsonElement element)
    {
        var link = ReadLink(element);
        if (!element.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(title.GetString()) || !element.TryGetProperty("labels", out var labels)
            || labels.ValueKind != JsonValueKind.Array
            || (element.TryGetProperty("pull_request", out var pull) && pull.ValueKind != JsonValueKind.Null))
        {
            throw new InvalidOperationException("Forgejo returned incomplete or unsupported issue metadata.");
        }

        return new ForgeIssue(link.Reference, title.GetString()!, link.IsOpen, null, [.. labels.EnumerateArray().Select(ReadLabel)], null, []);
    }

    private static string ReadLabel(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
        {
            throw new InvalidOperationException("Forgejo returned incomplete issue label metadata.");
        }

        return name.GetString()!;
    }

    private static ForgeIssueLink ReadLink(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("number", out var number)
            || number.ValueKind != JsonValueKind.Number || !number.TryGetInt64(out var index) || index < 1
            || !element.TryGetProperty("repository", out var repo) || repo.ValueKind != JsonValueKind.Object
            || !repo.TryGetProperty("full_name", out var fullName) || fullName.ValueKind != JsonValueKind.String
            || fullName.GetString() is not { } repository || !ForgejoRepositoryName.TryParse(repository, out _, out _)
            || !element.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String
            || state.GetString() is not ("open" or "closed"))
        {
            throw new InvalidOperationException("Forgejo returned inaccessible or incomplete issue metadata.");
        }

        return new ForgeIssueLink(new ForgeIssueReference(repository, index), state.GetString() == "open");
    }

    private static bool SameRepository(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string RepositoryIssuesPath(string repository)
    {
        var (owner, name) = ForgejoRepositoryName.Parse(repository, nameof(repository));
        return $"repos/{owner}/{name}/issues";
    }

    private static string IssuePath(string path, long number) => string.Create(CultureInfo.InvariantCulture, $"{path}/{number}");

    private static string Display(ForgeIssueReference reference) => string.Create(CultureInfo.InvariantCulture, $"{reference.Repository}#{reference.Number}");
}
