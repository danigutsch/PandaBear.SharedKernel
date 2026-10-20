using System.Net;
using System.Text;

namespace SharedKernel.HttpClients.Tests;

internal static class ContractCommandOutcomeTestResponses
{
    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
    }

    public static HttpResponseMessage Empty(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
    }
}
