namespace PandaBear.Repositories.Forgejo.Tests;

/// <summary>Builds the label list pages that a Forgejo instance answers with, including their <c>Link</c> header.</summary>
internal static class LinkedPages
{
    /// <summary>Returns a 200 OK JSON response with <paramref name="json"/> and the <c>Link</c> header <paramref name="link"/>.</summary>
    public static HttpResponseMessage Json(string json, string link)
    {
        var response = HttpResponseFactory.Json(json);
        response.Headers.TryAddWithoutValidation("Link", link);
        return response;
    }

    /// <summary>Returns a 200 OK JSON response with <paramref name="json"/> and the <c>X-Total-Count</c> header <paramref name="total"/>, as Forgejo 17 answers a label list.</summary>
    public static HttpResponseMessage Counted(string json, int total)
    {
        var response = HttpResponseFactory.Json(json);
        response.Headers.TryAddWithoutValidation("X-Total-Count", total.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return response;
    }
}
