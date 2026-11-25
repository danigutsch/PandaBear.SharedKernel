using System.Text.Json;

namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>Reads the JSON bodies the fake Umami received.</summary>
internal static class UmamiBodies
{
    /// <summary>Returns the property names of a JSON object in ordinal order.</summary>
    /// <param name="element">The object.</param>
    /// <returns>The names.</returns>
    internal static string[] PropertyNames(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    /// <summary>Returns the <c>payload.url</c> of a send body.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The URL.</returns>
    internal static string? UrlOf(string? body)
    {
        ArgumentNullException.ThrowIfNull(body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("payload").GetProperty("url").GetString();
    }
}
