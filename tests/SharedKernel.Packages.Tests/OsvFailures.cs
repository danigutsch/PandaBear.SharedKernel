using System.Net;

namespace PandaBear.Packages.Tests;

/// <summary>Scripted OSV failure scenarios for the failure-not-empty theory.</summary>
internal static class OsvFailures
{
    /// <summary>Answers a request according to <paramref name="scenario"/>; a batch scenario fails the batch, a vuln scenario the record read.</summary>
    public static HttpResponseMessage Respond(string scenario, RecordedRequest request)
    {
        if (request.Uri.AbsolutePath == "/v1/querybatch")
        {
            return scenario switch
            {
                "batch-500" => FakeHttpHandler.Json("secret-body", HttpStatusCode.InternalServerError),
                "batch-429" => FakeHttpHandler.Json("secret-body", HttpStatusCode.TooManyRequests),
                "batch-not-json" => FakeHttpHandler.Json("secret-body"),
                "batch-no-results" => FakeHttpHandler.Json("{}"),
                "batch-too-few-results" => FakeHttpHandler.Json("{\"results\":[]}"),
                "batch-null-result" => FakeHttpHandler.Json("{\"results\":[null]}"),
                "batch-id-missing" => FakeHttpHandler.Json("{\"results\":[{\"vulns\":[{\"modified\":\"x\"}]}]}"),
                "batch-id-unsafe" => FakeHttpHandler.Json("{\"results\":[{\"vulns\":[{\"id\":\"../../x\"}]}]}"),
                "batch-redirect" => new HttpResponseMessage(HttpStatusCode.MovedPermanently),
                "network-error" => throw new HttpRequestException("secret-body"),
                _ => FakeHttpHandler.Json(OsvAnswers.Batch([["GHSA-1"]])),
            };
        }

        return scenario switch
        {
            "vuln-404" => FakeHttpHandler.Json("secret-body", HttpStatusCode.NotFound),
            "vuln-500" => FakeHttpHandler.Json("secret-body", HttpStatusCode.InternalServerError),
            "vuln-not-json" => FakeHttpHandler.Json("secret-body"),
            _ => FakeHttpHandler.Json(OsvAnswers.RangeOnly("GHSA-OTHER", "a")),
        };
    }
}
