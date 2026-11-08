using System.Text.Json.Serialization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>
/// Represents a hypermedia link in a REST response. The JSON wire shape is
/// <c>{"rel":"self","href":"/items/1","method":"GET"}</c>. Deserializing a blank or missing value throws
/// <see cref="ArgumentException"/>, so an invalid link never becomes an instance.
/// </summary>
public sealed record HypermediaLink
{
    /// <summary>
    /// Initializes a link. Prefer <see cref="Create"/> for untrusted input; this constructor is the JSON deserialization entry point.
    /// </summary>
    /// <param name="rel">The link relation.</param>
    /// <param name="href">The link target.</param>
    /// <param name="method">The HTTP method to use on the target.</param>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    /// <exception cref="ArgumentException">A value is empty or whitespace.</exception>
    [JsonConstructor]
    public HypermediaLink(string rel, string href, string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rel);
        ArgumentException.ThrowIfNullOrWhiteSpace(href);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Rel = rel;
        Href = href;
        Method = method;
    }

    /// <summary>
    /// Gets the link relation, such as <c>self</c>.
    /// </summary>
    [JsonPropertyName("rel")]
    public string Rel
    {
        get;
    }

    /// <summary>
    /// Gets the link target.
    /// </summary>
    [JsonPropertyName("href")]
    public string Href
    {
        get;
    }

    /// <summary>
    /// Gets the HTTP method to use on the target.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method
    {
        get;
    }

    /// <summary>
    /// Creates a link, rejecting blank values.
    /// </summary>
    /// <param name="rel">The link relation.</param>
    /// <param name="href">The link target.</param>
    /// <param name="method">The HTTP method to use on the target.</param>
    /// <returns>A valid link, or a validation error naming the first blank field.</returns>
    public static Result<HypermediaLink> Create(string? rel, string? href, string? method)
    {
        if (string.IsNullOrWhiteSpace(rel))
        {
            return Blank("rel");
        }

        if (string.IsNullOrWhiteSpace(href))
        {
            return Blank("href");
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            return Blank("method");
        }

        return Result.Ok(new HypermediaLink(rel, href, method));
    }

    private static Result<HypermediaLink> Blank(string field)
    {
        var message = $"The link {field} must not be blank.";
        return Result.Invalid<HypermediaLink>(detail: message, field: field, message: message);
    }
}
