using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using SharedKernel.DevContainers;

namespace SharedKernel.Repositories;

/// <summary>Renders the pull request workflow selected by the caller's forge and pinned CI policy.</summary>
internal static class ForgeCiProfile
{
    private const string HttpsPrefix = "https://";

    private static readonly JsonDocumentOptions PolicyOptions = new() { AllowDuplicateProperties = false, MaxDepth = 4 };

    /// <summary>Gets the configuration directory for the selected forge.</summary>
    /// <exception cref="InvalidOperationException">The selected forge is unsupported.</exception>
    internal static string GetDirectory(ForgeKind forge) => forge switch
    {
        ForgeKind.GitHub => ".github",
        ForgeKind.Forgejo => ".forgejo",
        _ => throw new InvalidOperationException("Select github or forgejo for the forge profile.")
    };

    /// <summary>Validates the caller's JSON policy and renders the selected workflow.</summary>
    /// <exception cref="InvalidOperationException">The policy cannot be read or contains an invalid value.</exception>
    internal static string Render(ForgeKind forge, string policy)
    {
        using var document = Parse(policy);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Any(static property => property.Name is not
                ("runner" or "image" or "checkoutAction" or "checkAction" or "timeoutMinutes" or "githubActionOrigin")))
        {
            throw new InvalidOperationException("CI policy must be a JSON object with runner, image, checkoutAction, checkAction, timeoutMinutes and a GitHub action origin when selecting GitHub.");
        }

        var runner = GetString(root, "runner");
        if (runner is null or { Length: 0 or > 100 } || !char.IsAsciiLetterOrDigit(runner[0]) ||
            !runner.All(IsNameCharacter))
        {
            throw new InvalidOperationException("CI policy runner must be a simple runner label.");
        }

        var image = GetString(root, "image");
        if (!DevcontainerImageConfiguration.IsDigestPinned(image))
        {
            throw new InvalidOperationException("CI policy image must be pinned by a SHA-256 digest.");
        }

        var origin = ReadGitHubOrigin(root, forge);
        var checkout = ReadAction(root, "checkoutAction", origin);
        var check = ReadAction(root, "checkAction", origin);
        if (!root.TryGetProperty("timeoutMinutes", out var timeoutValue) || timeoutValue.ValueKind != JsonValueKind.Number ||
            !timeoutValue.TryGetInt32(out var timeout) || timeout is < 1 or > 360)
        {
            throw new InvalidOperationException("CI policy timeoutMinutes must be an integer from 1 to 360.");
        }

        // Fork pull requests receive a read-only token on Forgejo. Forgejo can ignore a job's permissions declaration.
        var permissions = forge == ForgeKind.GitHub ? "permissions:\n  contents: read\n" : string.Empty;
        var jobCondition = forge == ForgeKind.Forgejo
            ? "    if: ${{ forgejo.event.pull_request.head.repo.id && forgejo.event.pull_request.base.repo.id && (forgejo.event.pull_request.head.repo.id != forgejo.event.pull_request.base.repo.id) }}\n"
            : string.Empty;
        return $$$"""
            name: CI
            on:
              pull_request:
            {{{permissions}}}concurrency:
              group: ${{ github.workflow }}-${{ github.event.pull_request.number }}
              cancel-in-progress: true
            jobs:
              check:
            {{{jobCondition}}}    runs-on: "{{{runner}}}"
                timeout-minutes: {{{timeout.ToString(CultureInfo.InvariantCulture)}}}
                container:
                  image: "{{{image}}}"
                steps:
                  - uses: "{{{checkout}}}"
                    with:
                      persist-credentials: false
                  - uses: "{{{check}}}"
            """ + "\n";
    }

    private static JsonDocument Parse(string policy)
    {
        try
        {
            return JsonDocument.Parse(policy, PolicyOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("CI policy is not valid JSON.", exception);
        }
    }

    private static string? GetString(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadGitHubOrigin(JsonElement root, ForgeKind forge)
    {
        if (forge == ForgeKind.Forgejo)
        {
            return root.TryGetProperty("githubActionOrigin", out _)
                ? throw new InvalidOperationException("CI policy githubActionOrigin applies to GitHub workflows.")
                : null;
        }

        var origin = GetString(root, "githubActionOrigin");
        if (origin is null || !TryReadHttpsAddress(origin, out _) || origin.AsSpan(HttpsPrefix.Length).Contains('/'))
        {
            throw new InvalidOperationException("CI policy githubActionOrigin must be an absolute HTTPS authority without a path.");
        }

        return origin;
    }

    private static string ReadAction(JsonElement root, string field, string? githubOrigin)
    {
        var reference = GetString(root, field);
        if (reference is null || !reference.StartsWith(HttpsPrefix, StringComparison.Ordinal))
        {
            throw InvalidAction(field);
        }

        var at = reference.IndexOf('@', StringComparison.Ordinal);
        var pathStart = reference.IndexOf('/', HttpsPrefix.Length, StringComparison.Ordinal);
        if (pathStart < 0 || at <= pathStart || !ActionPinVerifier.IsFullCommitSha(reference.AsSpan(at + 1)))
        {
            throw InvalidAction(field);
        }

        var source = reference[..at];
        if (!TryReadHttpsAddress(source, out var uri) ||
            !string.Equals(source[pathStart..], uri.AbsolutePath, StringComparison.Ordinal))
        {
            throw InvalidAction(field);
        }

        var segments = source[(pathStart + 1)..].Split('/');
        if (segments.Length < 2 ||
            segments.Any(static segment => segment is "" or "." or ".." || !segment.All(IsNameCharacter)))
        {
            throw InvalidAction(field);
        }

        if (githubOrigin is null)
        {
            return reference;
        }

        if (!source.AsSpan(0, pathStart).Equals(githubOrigin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"CI policy {field} must match githubActionOrigin.");
        }

        if (!GitHubRepositoryName.IsValid(segments[0] + "/" + segments[1]))
        {
            throw InvalidAction(field);
        }

        return reference[(pathStart + 1)..];
    }

    private static bool TryReadHttpsAddress(string address, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        return address.StartsWith(HttpsPrefix, StringComparison.Ordinal) &&
            address.All(static value => IsNameCharacter(value) || value is ':' or '/' or '[' or ']') &&
            Uri.TryCreate(address, UriKind.Absolute, out uri) &&
            uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static bool IsNameCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-';

    private static InvalidOperationException InvalidAction(string field) =>
        new($"CI policy {field} must be an absolute HTTPS action URL pinned to a full 40-character lowercase commit SHA.");
}
