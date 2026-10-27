using System.Net;
using System.Text;

namespace PandaBear.HttpClients.Tests;

/// <summary>Factory for canned JSON <see cref="HttpResponseMessage" /> instances used by the HTTP outcome mapper tests.</summary>
internal static class HttpOutcomeMapperTestResponses
{
    /// <summary>Creates a response with the status code and a UTF-8 string body of the given media type.</summary>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="json">The body text.</param>
    /// <param name="mediaType">The body media type.</param>
    /// <returns>The response message.</returns>
    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
    }

    /// <summary>Creates a JSON response like <see cref="Json" /> and sets its <c>ETag</c> header.</summary>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="json">The body text.</param>
    /// <param name="entityTag">The entity tag text, parsed with its quotes.</param>
    /// <returns>The response message.</returns>
    public static HttpResponseMessage JsonWithETag(HttpStatusCode statusCode, string json, string entityTag)
    {
        var response = Json(statusCode, json);
        response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    /// <summary>Creates a response with the status code and an empty body declared as <c>application/json</c>.</summary>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>The response message.</returns>
    public static HttpResponseMessage Empty(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
    }
}
