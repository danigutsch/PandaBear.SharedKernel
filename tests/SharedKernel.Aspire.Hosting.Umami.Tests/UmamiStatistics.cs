using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PandaBear.Aspire.Hosting.Umami.Tests;

/// <summary>
/// Reads a website's statistics back from Umami 3.4.1 as its dashboard does: <c>POST /api/auth/login</c> as <c>admin</c> with the
/// default password, then <c>GET /api/websites/{id}/stats</c> (<c>src/app/api/websites/[websiteId]/stats/route.ts</c>).
/// </summary>
internal static class UmamiStatistics
{
    /// <summary>Counts the page views Umami recorded for a website since the epoch.</summary>
    /// <param name="umami">Umami's base address.</param>
    /// <param name="websiteId">The website id.</param>
    /// <param name="ct">Cancels the requests.</param>
    /// <returns>The number of page views.</returns>
    internal static async Task<long> PageViews(Uri umami, Guid websiteId, CancellationToken ct)
    {
        using var client = new HttpClient { BaseAddress = umami };
        using var login = await client.PostAsJsonAsync("/api/auth/login", new Dictionary<string, string> { ["username"] = "admin", ["password"] = "umami" }, ct);
        login.EnsureSuccessStatusCode();
        using var credentials = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct));
        var token = credentials.RootElement.GetProperty("token").GetString();

        var endAt = TimeProvider.System.GetUtcNow().AddMinutes(1).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/websites/{websiteId:D}/stats?startAt=0&endAt={endAt}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var stats = await client.SendAsync(request, ct);
        stats.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await stats.Content.ReadAsStringAsync(ct));
        var pageViews = body.RootElement.GetProperty("pageviews");
        return pageViews.ValueKind == JsonValueKind.String
            ? long.Parse(pageViews.GetString()!, CultureInfo.InvariantCulture)
            : pageViews.GetInt64();
    }
}
