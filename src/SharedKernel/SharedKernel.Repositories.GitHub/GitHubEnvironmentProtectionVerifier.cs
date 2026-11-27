using System.Globalization;
using System.Net;
using System.Text.Json;

namespace SharedKernel.Repositories.GitHub;

/// <summary>Verifies the required-reviewer and administrator-bypass settings of a GitHub environment.</summary>
public static class GitHubEnvironmentProtectionVerifier
{
    private const int MaxResponseBytes = 128 * 1024;

    /// <summary>Reads the exact environment and verifies its protection settings with a single bounded GET.</summary>
    /// <param name="client">A caller-owned GitHub client configured to refuse redirects.</param>
    /// <param name="repository">The GitHub repository as <c>owner/name</c>.</param>
    /// <param name="environment">The exact environment name.</param>
    /// <param name="ct">A token that cancels the verification.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The repository or environment name is invalid.</exception>
    /// <exception cref="InvalidOperationException">The response cannot establish the required settings.</exception>
    /// <exception cref="TimeoutException">The required settings could not be read before the deadline.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct" /> was canceled.</exception>
    public static async Task Verify(HttpClient client, string repository, string environment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new ArgumentException("The GitHub repository must be a valid owner/name.", nameof(repository));
        }

        if (environment is "." or ".." || environment.Any(char.IsControl))
        {
            throw new ArgumentException("The GitHub environment name is invalid.", nameof(environment));
        }

        var uri = new Uri($"https://api.github.com/repos/{repository}/environments/{Uri.EscapeDataString(environment)}", UriKind.Absolute);
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"GitHub environment protection request failed with HTTP {(int)response.StatusCode}."));
            }

            var body = await BoundedHttpContentReader.Read(response.Content, MaxResponseBytes,
                "GitHub environment protection response exceeded the size limit.", requestCancellation.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { AllowDuplicateProperties = false });
            VerifyResponse(document.RootElement, environment);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("environment protection");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            throw new InvalidOperationException("GitHub environment protection request failed.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("GitHub environment protection response is malformed.");
        }
    }

    private static void VerifyResponse(JsonElement response, string environment)
    {
        if (!TryGetString(response, "name", out var name) || !name.ValueEquals(environment))
        {
            throw new InvalidOperationException("GitHub environment protection response did not contain the exact environment name.");
        }

        if (!response.TryGetProperty("can_admins_bypass", out var bypass) || bypass.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException("GitHub environment protection does not explicitly disable administrator bypass.");
        }

        if (!response.TryGetProperty("protection_rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GitHub environment protection rules are absent or malformed.");
        }

        var hasRequiredReviewers = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (!TryGetString(rule, "type", out var type))
            {
                throw new InvalidOperationException("GitHub environment protection rules are malformed.");
            }

            if (type.ValueEquals("required_reviewers"))
            {
                if (hasRequiredReviewers || !HasValidReviewers(rule))
                {
                    throw new InvalidOperationException("GitHub environment protection required reviewer settings are malformed or empty.");
                }

                hasRequiredReviewers = true;
            }
        }

        if (!hasRequiredReviewers)
        {
            throw new InvalidOperationException("GitHub environment protection has no configured required reviewer.");
        }
    }

    private static bool HasValidReviewers(JsonElement rule) =>
        rule.TryGetProperty("reviewers", out var reviewers)
        && reviewers.ValueKind == JsonValueKind.Array
        && reviewers.GetArrayLength() > 0
        && reviewers.EnumerateArray().All(IsValidReviewer);

    private static bool IsValidReviewer(JsonElement entry) =>
        TryGetString(entry, "type", out var type)
        && (type.ValueEquals("User") || type.ValueEquals("Team"))
        && entry.TryGetProperty("reviewer", out var reviewer)
        && reviewer.ValueKind == JsonValueKind.Object
        && reviewer.TryGetProperty("id", out var id)
        && id.ValueKind == JsonValueKind.Number
        && id.TryGetInt64(out var idNumber)
        && idNumber > 0;

    private static bool TryGetString(JsonElement container, string name, out JsonElement value)
    {
        value = default;
        return container.ValueKind == JsonValueKind.Object
            && container.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.String;
    }
}
