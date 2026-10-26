using System.Net;
using System.Text;

namespace PandaBear.HttpClients.Tests;

internal static class HttpOutcomeMapperTestResponses
{
    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
    }

    public static HttpResponseMessage JsonWithETag(HttpStatusCode statusCode, string json, string entityTag)
    {
        var response = Json(statusCode, json);
        response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    public static HttpResponseMessage Empty(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
    }
}
