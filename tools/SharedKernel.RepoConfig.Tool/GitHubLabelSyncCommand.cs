using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Creates the configured GitHub labels that are missing and updates those that differ. Labels that are not configured
/// are kept.
/// </summary>
internal static class GitHubLabelSyncCommand
{
    private const string ApiVersion = "2022-11-28";
    private const int MaxResponseBytes = 1024 * 1024;
    private const int PageSize = 100;
    private const int MaxPages = 100;

    /// <summary>
    /// Reads the configured labels and the repository's labels, then previews the changes, or with
    /// <paramref name="force"/> writes them.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository or configuration is invalid, no token is set, or GitHub rejects a request or returns an invalid
    /// response.
    /// </exception>
    /// <exception cref="TimeoutException">GitHub does not answer a request within the request timeout.</exception>
    public static Task<int> Run(
        string rootPath,
        string repository,
        bool force,
        HttpClient? httpClient,
        TextWriter output,
        CancellationToken cancellationToken) =>
        Run(rootPath, repository, force, httpClient, TimeProvider.System, output, cancellationToken);

    internal static async Task<int> Run(
        string rootPath,
        string repository,
        bool force,
        HttpClient? httpClient,
        TimeProvider timeProvider,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(output);

        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new InvalidOperationException("--repository must name a GitHub repository as owner/name.");
        }

        var configuration = await ConsumerConfigurationFile.Read(rootPath, cancellationToken).ConfigureAwait(false);
        var labels = configuration.Labels
            ?? throw new InvalidOperationException($"{ConsumerConfigurationFile.RelativePath} has no labels section.");
        using var ownedHttpClient = httpClient is null ? GitHubHttpClient.Create("label sync") : null;
        var client = httpClient ?? ownedHttpClient!;
        var parts = repository.Split('/');
        var labelsUri = $"https://api.github.com/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/labels";
        var remote = await ReadLabels(client, labelsUri, timeProvider, cancellationToken).ConfigureAwait(false);

        int created = 0, updated = 0;
        foreach (var label in labels)
        {
            if (remote.TryGetValue(label.Name, out var current) && IsCurrent(label, current))
            {
                continue;
            }

            var create = current is null;
            if (create)
            {
                created++;
            }
            else
            {
                updated++;
            }

            if (!force)
            {
                await output.WriteLineAsync($"preview: would {(create ? "create" : "update")} label {label.Name}.".AsMemory(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            await (create
                ? Write(client, HttpMethod.Post, new Uri(labelsUri), label, "name", HttpStatusCode.Created, timeProvider, cancellationToken)
                : Write(client, HttpMethod.Patch, new Uri(labelsUri + "/" + Uri.EscapeDataString(current!.Name)), label, "new_name", HttpStatusCode.OK, timeProvider, cancellationToken))
                .ConfigureAwait(false);
            await output.WriteLineAsync($"{(create ? "Created" : "Updated")} label {label.Name}.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var unchanged = labels.Count - created - updated;
        await output.WriteLineAsync(
            (force
                ? string.Create(CultureInfo.InvariantCulture, $"{repository}: {created} created, {updated} updated, and {unchanged} unchanged labels.")
                : string.Create(CultureInfo.InvariantCulture, $"preview: {repository} would get {created} created, {updated} updated, and {unchanged} unchanged labels; --force writes them.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    // GitHub stores colors in lowercase and an absent description as null.
    private static bool IsCurrent(GitHubLabelConfiguration label, GitHubLabelConfiguration current) =>
        string.Equals(label.Name, current.Name, StringComparison.Ordinal)
        && string.Equals(label.Color, current.Color, StringComparison.OrdinalIgnoreCase)
        && string.Equals(label.Description, current.Description, StringComparison.Ordinal);

    private static async Task<Dictionary<string, GitHubLabelConfiguration>> ReadLabels(
        HttpClient client,
        string labelsUri,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Dictionary<string, GitHubLabelConfiguration> labels = new(StringComparer.OrdinalIgnoreCase);
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

    private static int ReadPage(byte[] content, Dictionary<string, GitHubLabelConfiguration> labels)
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

                var label = new GitHubLabelConfiguration(name.GetString()!, color.GetString()!, description.GetString() ?? string.Empty);
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
        GitHubLabelConfiguration label,
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
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
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
