using System.Net;
using System.Text;
using System.Text.Json;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>
/// Answers the three Umami 3.4.1 routes that provisioning uses, as the Umami source does: <c>POST /api/auth/login</c>,
/// <c>GET /api/websites/{id}</c> (<c>null</c> for an unknown website) and <c>POST /api/websites</c>. It records every request.
/// </summary>
internal sealed class FakeUmami : HttpMessageHandler
{
    /// <summary>The token a successful sign-in returns, which must never reach a log.</summary>
    internal const string Token = "token-secret";

    private readonly Lock _gate = new();
    private readonly List<RecordedRequest> _requests = [];

    /// <summary>Gets or sets the password of the <c>admin</c> user; <c>umami</c>, Umami's default, unless changed.</summary>
    internal string AdminPassword { get; set; } = "umami";

    /// <summary>Gets or sets a value indicating whether the admin user has two-factor sign-in.</summary>
    internal bool RequiresTwoFactor { get; set; }

    /// <summary>Gets or sets the status that creating a website answers with instead of success, or <see langword="null"/>.</summary>
    internal HttpStatusCode? CreateFailure { get; set; }

    /// <summary>Gets the ids of the websites that exist.</summary>
    internal HashSet<Guid> Websites { get; } = [];

    /// <summary>Gets the requests received so far.</summary>
    internal IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var authorization = request.Headers.Authorization?.ToString();
        lock (_gate)
        {
            _requests.Add(new RecordedRequest(request.Method, path, authorization, body));
        }

        if (request.Method == HttpMethod.Post && path == "/api/auth/login")
        {
            return SignIn(body);
        }

        if (authorization != $"Bearer {Token}")
        {
            return Error(HttpStatusCode.Unauthorized);
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/websites/", StringComparison.Ordinal))
        {
            var id = Guid.Parse(path["/api/websites/".Length..]);
            return Websites.Contains(id) ? Json($"{{\"id\":\"{id}\",\"name\":\"site\"}}") : Json("null");
        }

        if (request.Method == HttpMethod.Post && path == "/api/websites")
        {
            if (CreateFailure is { } failure)
            {
                return Error(failure);
            }

            using var created = JsonDocument.Parse(body!);
            var id = created.RootElement.GetProperty("id").GetGuid();
            Websites.Add(id);
            return Json($"{{\"id\":\"{id}\",\"shareId\":null}}");
        }

        return Error(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage SignIn(string? body)
    {
        using var credentials = JsonDocument.Parse(body!);
        var username = credentials.RootElement.GetProperty("username").GetString();
        var password = credentials.RootElement.GetProperty("password").GetString();
        if (username != "admin" || password != AdminPassword)
        {
            return Error(HttpStatusCode.Unauthorized);
        }

        return RequiresTwoFactor
            ? Json("{\"requiresTwoFactor\":true,\"partialToken\":\"partial\"}")
            : Json($"{{\"token\":\"{Token}\",\"user\":{{\"username\":\"admin\",\"isAdmin\":true}}}}");
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status) =>
        new(status) { Content = new StringContent($"{{\"error\":{{\"status\":{(int)status}}}}}", Encoding.UTF8, "application/json") };

    /// <summary>One request the fake received.</summary>
    /// <param name="Method">The method.</param>
    /// <param name="Path">The path.</param>
    /// <param name="Authorization">The <c>Authorization</c> header, or <see langword="null"/>.</param>
    /// <param name="Body">The body, or <see langword="null"/>.</param>
    internal sealed record RecordedRequest(HttpMethod Method, string Path, string? Authorization, string? Body);
}
