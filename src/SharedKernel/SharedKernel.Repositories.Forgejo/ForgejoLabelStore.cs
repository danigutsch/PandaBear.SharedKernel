using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PandaBear.Forgejo;
using PandaBear.IO;

namespace PandaBear.Repositories.Forgejo;

/// <summary>
/// The labels of a Forgejo repository, read and written through the labels API of
/// <see href="https://code.forgejo.org/api/swagger">the Forgejo REST API</see>: <c>GET</c> and <c>POST</c>
/// <c>/repos/{owner}/{repo}/labels</c>, and <c>PATCH /repos/{owner}/{repo}/labels/{id}</c>.
/// </summary>
/// <remarks>
/// Forgejo edits a label by its numeric ID, not by its name, so <see cref="Update"/> uses the ID that the last
/// <see cref="List"/> read for the name.
/// </remarks>
public sealed class ForgejoLabelStore : IRepositoryLabelStore, IDisposable
{
    private const int MaxPages = 100;
    private const int MaxResponseBytes = 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly string _labelsPath;
    private readonly Dictionary<string, long> _ids = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the store of the labels of <paramref name="repository"/> on <paramref name="server"/>.</summary>
    /// <param name="handler">Sends the requests; the store disposes it. It should refuse redirects, so the token never reaches another address.</param>
    /// <param name="server">The HTTPS address of the Forgejo instance, such as <c>https://codeberg.org</c>.</param>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="userAgent">Names the caller, such as <c>sharedkernel-repo/1.0</c>.</param>
    /// <param name="token">The token from <see cref="ForgejoToken.Find"/>, which writing requires, or <see langword="null"/> to read the labels of a public repository anonymously.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>, <paramref name="server"/>, <paramref name="repository"/>, or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> is not an absolute HTTPS address without user information, a query, or a fragment, or <paramref name="token"/> is empty or white-space.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="repository"/> does not name a Forgejo repository as <c>owner/name</c>.</exception>
    public ForgejoLabelStore(HttpMessageHandler handler, Uri server, string repository, ProductInfoHeaderValue userAgent, string? token)
        : this(handler, server, repository, userAgent, token, TimeProvider.System)
    {
    }

    /// <summary>Creates the store with an explicit time source for the request timeout.</summary>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="token"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> or <paramref name="token"/> is not valid.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="repository"/> does not name a Forgejo repository as <c>owner/name</c>.</exception>
    internal ForgejoLabelStore(
        HttpMessageHandler handler,
        Uri server,
        string repository,
        ProductInfoHeaderValue userAgent,
        string? token,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userAgent);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!ForgejoRepositoryName.TryParse(repository, out var owner, out var name))
        {
            throw new InvalidOperationException("--repository must name a Forgejo repository as owner/name.");
        }

        Repository = repository;
        _labelsPath = "repos/" + Uri.EscapeDataString(owner) + "/" + Uri.EscapeDataString(name) + "/labels";
        _timeProvider = timeProvider;
        _client = ForgejoApiClient.Create(handler, server, userAgent, token);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc />
    public string Repository { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Reads <c>GET /repos/{owner}/{repo}/labels</c> with <c>limit=50</c> through <see cref="ForgejoPager"/>, which follows
    /// the <c>Link</c> header, or the <c>X-Total-Count</c> header without one (Forgejo 17 sends no <c>Link</c> header for
    /// this list), or else reads to the first empty page, and fails above 100 pages. Colors are returned without a leading <c>#</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Forgejo rejects a request or returns an invalid response, or the repository has more than 100 pages of labels.</exception>
    /// <exception cref="TimeoutException">Forgejo does not answer a request within 30 seconds.</exception>
    public async Task<IReadOnlyList<RepositoryLabel>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<(long Id, RepositoryLabel Label)> entries;
        try
        {
            entries = await ForgejoPager.ReadAll(ReadPage, MaxPages, "The labels of " + Repository, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException exception)
        {
            // Every listing failure of this store is an InvalidOperationException, which the label sync tool reports.
            throw new InvalidOperationException(exception.Message, exception);
        }

        Dictionary<string, RepositoryLabel> labels = new(StringComparer.OrdinalIgnoreCase);
        _ids.Clear();
        foreach (var (id, label) in entries)
        {
            if (labels.TryAdd(label.Name, label))
            {
                _ids[label.Name] = id;
            }
        }

        return [.. labels.Values];
    }

    /// <inheritdoc />
    /// <remarks>Sends <c>POST /repos/{owner}/{repo}/labels</c> with the name, the color as <c>#rrggbb</c>, and the description; any answer but 201 Created fails.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Forgejo rejects the request.</exception>
    /// <exception cref="TimeoutException">Forgejo does not answer within 30 seconds.</exception>
    public Task Create(RepositoryLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        return Write(HttpMethod.Post, new Uri(_client.BaseAddress!, _labelsPath), label, HttpStatusCode.Created, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends <c>PATCH /repos/{owner}/{repo}/labels/{id}</c> with the name, the color as <c>#rrggbb</c>, and the
    /// description, for the ID that <see cref="List"/> read for <paramref name="currentName"/>; any answer but 200 OK fails.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="currentName"/> or <paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="List"/> has not read a label named <paramref name="currentName"/>, or Forgejo rejects the request.</exception>
    /// <exception cref="TimeoutException">Forgejo does not answer within 30 seconds.</exception>
    public Task Update(string currentName, RepositoryLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentName);
        ArgumentNullException.ThrowIfNull(label);
        if (!_ids.TryGetValue(currentName, out var id))
        {
            throw new InvalidOperationException($"Forgejo label sync has not read label {currentName}; list the labels before updating one.");
        }

        var uri = new Uri(_client.BaseAddress!, _labelsPath + "/" + id.ToString(CultureInfo.InvariantCulture));
        return Write(HttpMethod.Patch, uri, label, HttpStatusCode.OK, cancellationToken);
    }

    /// <summary>Disposes the client and its handler.</summary>
    public void Dispose() => _client.Dispose();

    private static List<(long Id, RepositoryLabel Label)> ReadLabels(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw InvalidResponse();
            }

            List<(long Id, RepositoryLabel Label)> labels = [];
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("id", out var id) || !id.TryGetInt64(out var labelId) || labelId < 1
                    || !element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                    || !element.TryGetProperty("color", out var color) || color.ValueKind != JsonValueKind.String
                    || (element.TryGetProperty("description", out var description) && description.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
                {
                    throw InvalidResponse();
                }

                labels.Add((labelId, new RepositoryLabel(
                    name.GetString()!,
                    color.GetString()!.TrimStart('#'),
                    element.TryGetProperty("description", out var text) ? text.GetString() ?? string.Empty : string.Empty)));
            }

            return labels;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw InvalidResponse();
        }
    }

    private static InvalidOperationException InvalidResponse() => new("Forgejo label sync returned an invalid label response.");

    private async Task<ForgejoPage<(long Id, RepositoryLabel Label)>> ReadPage(int page, CancellationToken cancellationToken)
    {
        var uri = new Uri(_client.BaseAddress!, string.Create(CultureInfo.InvariantCulture, $"{_labelsPath}?limit={ForgejoPager.PageSize}&page={page}"));
        var (content, headers) = await Send(HttpMethod.Get, uri, null, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
        return ForgejoPager.Page(ReadLabels(content), headers);
    }

    private async Task Write(HttpMethod method, Uri uri, RepositoryLabel label, HttpStatusCode expectedStatus, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("name", label.Name);
            writer.WriteString("color", "#" + label.Color);
            writer.WriteString("description", label.Description);
            writer.WriteEndObject();
        }

        _ = await Send(method, uri, stream.ToArray(), expectedStatus, label.Name, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(byte[] Content, HttpResponseHeaders Headers)> Send(
        HttpMethod method,
        Uri uri,
        byte[]? body,
        HttpStatusCode expectedStatus,
        string? labelName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };
        }

        using var timeout = new CancellationTokenSource(RequestTimeout, _timeProvider);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token).ConfigureAwait(false);
            if (response.StatusCode != expectedStatus)
            {
                // Only the status code is reported; the response can echo request details.
                throw new InvalidOperationException(labelName is null
                    ? string.Create(CultureInfo.InvariantCulture, $"Forgejo label sync failed with HTTP {(int)response.StatusCode}.")
                    : string.Create(CultureInfo.InvariantCulture, $"Forgejo label sync failed with HTTP {(int)response.StatusCode} for label {labelName}. Rerun the sync after fixing the cause."));
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw new InvalidOperationException("Forgejo label sync response exceeded the allowed size.");
            }

            var source = await response.Content.ReadAsStreamAsync(requestCancellation.Token).ConfigureAwait(false);
            byte[]? content;
            await using (source.ConfigureAwait(false))
            {
                content = await BoundedReader.ReadToEnd(source, MaxResponseBytes, requestCancellation.Token).ConfigureAwait(false);
            }

            return (content ?? throw new InvalidOperationException("Forgejo label sync response exceeded the allowed size."), response.Headers);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"Forgejo label sync did not answer within {RequestTimeout.TotalSeconds} seconds."));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // The transport message can carry request details, so it is never echoed.
            throw new InvalidOperationException("Forgejo label sync request failed.");
        }
    }
}
