using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PandaBear.GitHub;

namespace PandaBear.Repositories.GitHub;

/// <summary>
/// The labels of a GitHub repository, read and written through the
/// <see href="https://docs.github.com/rest/issues/labels">REST labels API</see>.
/// </summary>
public sealed class GitHubLabelStore : IRepositoryLabelStore
{
    private const int MaxResponseBytes = 1024 * 1024;
    private const int PageSize = 100;
    private const int MaxPages = 100;
    private readonly GitHubConnection _connection;
    private readonly TimeProvider _timeProvider;
    private readonly string _labelsUri;

    /// <summary>Creates the store for <paramref name="repository"/> that sends its requests through <paramref name="connection"/>.</summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="connection">The GitHub connection, which the caller disposes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repository"/> or <paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="repository"/> does not name a GitHub repository as <c>owner/name</c>.</exception>
    public GitHubLabelStore(string repository, GitHubConnection connection)
        : this(repository, connection, TimeProvider.System)
    {
    }

    /// <summary>Creates the store with an explicit time source for the request timeout.</summary>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="repository"/> does not name a GitHub repository as <c>owner/name</c>.</exception>
    internal GitHubLabelStore(string repository, GitHubConnection connection, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new InvalidOperationException("--repository must name a GitHub repository as owner/name.");
        }

        var parts = repository.Split('/');
        Repository = repository;
        _connection = connection;
        _timeProvider = timeProvider;
        _labelsUri = $"https://api.github.com/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/labels";
    }

    /// <inheritdoc />
    public string Repository { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">No token is available, or GitHub rejects the request or returns an invalid response.</exception>
    /// <exception cref="TimeoutException">GitHub does not answer a request within the request timeout.</exception>
    public async Task<IReadOnlyList<RepositoryLabel>> List(CancellationToken cancellationToken) =>
        [.. (await ReadLabels(_connection.Client, _labelsUri, _timeProvider, cancellationToken).ConfigureAwait(false)).Values];

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No token is available, or GitHub rejects the request.</exception>
    /// <exception cref="TimeoutException">GitHub does not answer within the request timeout.</exception>
    public Task Create(RepositoryLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        return Write(_connection.Client, HttpMethod.Post, new Uri(_labelsUri), label, "name", HttpStatusCode.Created, _timeProvider, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="currentName"/> or <paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No token is available, or GitHub rejects the request.</exception>
    /// <exception cref="TimeoutException">GitHub does not answer within the request timeout.</exception>
    public Task Update(string currentName, RepositoryLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentName);
        ArgumentNullException.ThrowIfNull(label);
        return Write(_connection.Client, HttpMethod.Patch, new Uri(_labelsUri + "/" + Uri.EscapeDataString(currentName)), label, "new_name", HttpStatusCode.OK, _timeProvider, cancellationToken);
    }

    private static async Task<Dictionary<string, RepositoryLabel>> ReadLabels(
        HttpClient client,
        string labelsUri,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Dictionary<string, RepositoryLabel> labels = new(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; page <= MaxPages; page++)
        {
            var uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"{labelsUri}?per_page={PageSize}&page={page}"));
            var content = await Send(client, HttpMethod.Get, uri, null, HttpStatusCode.OK, null, timeProvider, cancellationToken).ConfigureAwait(false);
            var count = ReadPage(content, labels);
            if (count < PageSize)
            {
                return labels;
            }
        }

        throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"GitHub label sync reads at most {MaxPages * PageSize} repository labels."));
    }

    private static int ReadPage(byte[] content, Dictionary<string, RepositoryLabel> labels)
    {
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw InvalidResponse();
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                    || !element.TryGetProperty("color", out var color) || color.ValueKind != JsonValueKind.String
                    || !element.TryGetProperty("description", out var description) || description.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    throw InvalidResponse();
                }

                var label = new RepositoryLabel(name.GetString()!, color.GetString()!, description.GetString() ?? string.Empty);
                labels.TryAdd(label.Name, label);
            }

            return document.RootElement.GetArrayLength();
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    private static async Task Write(
        HttpClient client,
        HttpMethod method,
        Uri uri,
        RepositoryLabel label,
        string nameProperty,
        HttpStatusCode expectedStatus,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(nameProperty, label.Name);
            writer.WriteString("color", label.Color);
            writer.WriteString("description", label.Description);
            writer.WriteEndObject();
        }

        _ = await Send(client, method, uri, stream.ToArray(), expectedStatus, label.Name, timeProvider, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> Send(
        HttpClient client,
        HttpMethod method,
        Uri uri,
        byte[]? body,
        HttpStatusCode expectedStatus,
        string? labelName,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiClient.ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };
        }

        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration, timeProvider);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token).ConfigureAwait(false);
            if (response.StatusCode != expectedStatus)
            {
                // Only the status code is reported; the response can echo request details.
                throw new InvalidOperationException(labelName is null
                    ? string.Create(CultureInfo.InvariantCulture, $"GitHub label sync failed with HTTP {(int)response.StatusCode}.")
                    : string.Create(CultureInfo.InvariantCulture, $"GitHub label sync failed with HTTP {(int)response.StatusCode} for label {labelName}. Rerun the sync after fixing the cause."));
            }

            return await BoundedHttpContentReader.Read(
                response.Content,
                MaxResponseBytes,
                "GitHub label sync response exceeded the allowed size.",
                requestCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("label sync");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // The transport message can carry request details, so it is never echoed.
            throw new InvalidOperationException("GitHub label sync request failed.");
        }
    }

    private static InvalidOperationException InvalidResponse() => new("GitHub label sync returned an invalid label response.");
}
