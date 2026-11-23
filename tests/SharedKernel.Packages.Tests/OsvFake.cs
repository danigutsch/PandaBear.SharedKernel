using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Packages.Tests;

/// <summary>Builds fake api.osv.dev handlers.</summary>
internal static class OsvFake
{
    /// <summary>The time source's start.</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Answers each query of a batch with the IDs that <paramref name="idsFor"/> returns for its package and version, and each record from <paramref name="records"/>.</summary>
    public static RecordingHttpMessageHandler Handler(Func<string, string, string[]?> idsFor, Dictionary<string, string> records) =>
        new RecordingHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.Message.RequestUri!.AbsolutePath == "/v1/querybatch")
            {
                return HttpResponseFactory.Json(OsvAnswers.Batch([.. Queries(request.Body!).Select(q => idsFor(q.Name, q.Version))]));
            }

            var id = Uri.UnescapeDataString(request.Message.RequestUri!.AbsolutePath["/v1/vulns/".Length..]);
            return records.TryGetValue(id, out var record) ? HttpResponseFactory.Json(record) : HttpResponseFactory.Json("{\"code\":5}", HttpStatusCode.NotFound);
        });

    /// <summary>Parses the queries of a batch request body.</summary>
    public static (string Name, string Ecosystem, string Version, string? PageToken)[] Queries(string body)
    {
        using var document = JsonDocument.Parse(body);
        return
        [
            .. document.RootElement.GetProperty("queries").EnumerateArray().Select(static q => (
                q.GetProperty("package").GetProperty("name").GetString()!,
                q.GetProperty("package").GetProperty("ecosystem").GetString()!,
                q.GetProperty("version").GetString()!,
                q.TryGetProperty("page_token", out var token) ? token.GetString() : null)),
        ];
    }

    /// <summary>Creates the source over <paramref name="client"/>.</summary>
    public static OsvAdvisorySource Source(HttpClient client, TimeProvider? time = null, string? apt = null, TimeSpan? lifetime = null, TimeSpan? timeout = null) =>
        new(client, time ?? new FakeTimeProvider(Start), apt, lifetime, timeout);
}
