using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Unicode;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Submits a dependency snapshot to the GitHub dependency submission API, or previews the submission.
/// </summary>
internal static class DependencySubmitCommand
{
    private const string ApiVersion = "2022-11-28";
    private const int MaxResponseBytes = 1024 * 1024;

    // Every snapshot that "dependency snapshot" writes fits: its JSON budget and the line break that ends the file.
    private const int MaxSnapshotBytes = DependencySnapshotCommand.MaximumOutputBytes + 2;

    /// <summary>
    /// Validates the repository and the snapshot, then previews the submission, or with <see cref="DependencySubmitOptions.Force"/>
    /// posts the snapshot bytes unchanged. Only a forced submission reads a token.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository or snapshot is invalid, no token is set, or GitHub rejects the request or reports the snapshot as
    /// invalid.
    /// </exception>
    /// <exception cref="TimeoutException">GitHub does not respond within the request timeout.</exception>
    public static Task<int> Run(
        DependencySubmitOptions options,
        HttpClient? httpClient,
        TextWriter output,
        CancellationToken cancellationToken) =>
        Run(options, httpClient, TimeProvider.System, output, cancellationToken);

    internal static async Task<int> Run(
        DependencySubmitOptions options,
        HttpClient? httpClient,
        TimeProvider timeProvider,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(output);

        if (!GitHubRepositoryName.IsValid(options.Repository))
        {
            throw new InvalidOperationException("--repository must name a GitHub repository as owner/name.");
        }

        var snapshot = await ReadSnapshot(options.RootPath, options.SnapshotPath, cancellationToken).ConfigureAwait(false);
        var parts = options.Repository.Split('/');
        var endpoint = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/dependency-graph/snapshots",
            UriKind.Absolute);
        if (!options.Force)
        {
            await output.WriteLineAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"preview: dependency submission would send {RepoConfigToolApplication.EscapeControlCharacters(options.SnapshotPath)} ({snapshot.Length} bytes) to {endpoint.AbsoluteUri}.").AsMemory(),
                cancellationToken).ConfigureAwait(false);
            return 0;
        }

        using var ownedHttpClient = httpClient is null ? GitHubHttpClient.Create("dependency submission") : null;
        var result = await Submit(httpClient ?? ownedHttpClient!, endpoint, snapshot, timeProvider, cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync(
            (result is "SUCCESS" or "ACCEPTED"
                ? $"Dependency snapshot submitted to {options.Repository}: {result}."
                : $"Dependency snapshot submitted to {options.Repository}.").AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<byte[]> ReadSnapshot(string root, string snapshotPath, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(Path.GetFullPath(snapshotPath, root), MaxSnapshotBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{snapshotPath} does not exist.", exception);
        }

        if (content is null)
        {
            throw new InvalidOperationException($"{snapshotPath} exceeds the 32 MiB snapshot size limit.");
        }

        if (!Utf8.IsValid(content))
        {
            throw new InvalidOperationException($"{snapshotPath}: {JsonVerifier.DescribeInvalidUtf8(content)}");
        }

        JsonValueKind kind;
        try
        {
            using var document = JsonDocument.Parse(content);
            kind = document.RootElement.ValueKind;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{snapshotPath}: {JsonVerifier.DescribeInvalidJson(exception)}", exception);
        }

        return kind == JsonValueKind.Object
            ? content
            : throw new InvalidOperationException($"{snapshotPath} must contain a JSON object.");
    }

    private static async Task<string?> Submit(
        HttpClient httpClient,
        Uri endpoint,
        byte[] snapshot,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        request.Content = new ByteArrayContent(snapshot);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration, timeProvider);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Only the status code is reported; the response can echo request details.
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"GitHub dependency submission failed with HTTP {(int)response.StatusCode}."));
            }

            var content = await BoundedHttpContentReader.Read(
                response.Content,
                MaxResponseBytes,
                "GitHub dependency submission response exceeded the allowed size.",
                requestCancellation.Token).ConfigureAwait(false);
            var result = ReadResult(content);
            return result == "INVALID"
                ? throw new InvalidOperationException("GitHub reported the dependency snapshot as invalid.")
                : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("dependency submission");
        }
        catch (HttpRequestException)
        {
            // The transport message can carry request details, so it is never echoed.
            throw new InvalidOperationException("GitHub dependency submission request failed.");
        }
    }

    // GitHub reports SUCCESS, ACCEPTED, or INVALID; a response without a readable result leaves the status code as the outcome.
    private static string? ReadResult(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.String
                    ? result.GetString()
                    : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
