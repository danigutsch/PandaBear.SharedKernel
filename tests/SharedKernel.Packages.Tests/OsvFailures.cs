using System.Net;

namespace SharedKernel.Packages.Tests;

/// <summary>Scripted OSV failure scenarios for the failure-not-empty theory.</summary>
internal static class OsvFailures
{
    /// <summary>Answers a request according to <paramref name="scenario"/>; a batch scenario fails the batch, a vuln scenario the record read.</summary>
    public static HttpResponseMessage Respond(string scenario, CapturedHttpRequest request)
    {
        if (request.Message.RequestUri!.AbsolutePath == "/v1/querybatch")
        {
            return scenario switch
            {
                "batch-500" => HttpResponseFactory.Json("secret-body", HttpStatusCode.InternalServerError),
                "batch-429" => HttpResponseFactory.Json("secret-body", HttpStatusCode.TooManyRequests),
                "batch-not-json" => HttpResponseFactory.Json("secret-body"),
                "batch-no-results" => HttpResponseFactory.Json("{}"),
                "batch-too-few-results" => HttpResponseFactory.Json("{\"results\":[]}"),
                "batch-null-result" => HttpResponseFactory.Json("{\"results\":[null]}"),
                "batch-id-missing" => HttpResponseFactory.Json("{\"results\":[{\"vulns\":[{\"modified\":\"x\"}]}]}"),
                "batch-id-unsafe" => HttpResponseFactory.Json("{\"results\":[{\"vulns\":[{\"id\":\"../../x\"}]}]}"),
                "batch-redirect" => new HttpResponseMessage(HttpStatusCode.MovedPermanently),
                "network-error" => throw new HttpRequestException("secret-body"),
                _ => HttpResponseFactory.Json(OsvAnswers.Batch([["GHSA-1"]])),
            };
        }

        return scenario switch
        {
            "vuln-404" => HttpResponseFactory.Json("secret-body", HttpStatusCode.NotFound),
            "vuln-500" => HttpResponseFactory.Json("secret-body", HttpStatusCode.InternalServerError),
            "vuln-not-json" => HttpResponseFactory.Json("secret-body"),
            _ => HttpResponseFactory.Json(OsvAnswers.RangeOnly("GHSA-OTHER", "a")),
        };
    }
}
