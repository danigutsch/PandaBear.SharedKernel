using System.Globalization;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The licence of a third-party image: a code as people read it, such as <c>CC BY 2.0</c>, <c>CC BY-SA 4.0</c>, <c>CC0 1.0</c>,
/// <c>Pixabay</c> or an application's own, and an optional https link to its text. <see cref="KnownUrl"/> gives the link of the common
/// Creative Commons codes.
/// </summary>
public sealed class ImageLicense : ValueObject
{
    /// <summary>The longest code.</summary>
    public const int MaxCodeLength = 60;

    private static readonly string[] CreativeCommonsElements = ["by", "by-sa", "by-nd", "by-nc", "by-nc-sa", "by-nc-nd"];
    private static readonly string[] CreativeCommonsVersions = ["2.0", "2.5", "3.0", "4.0"];

    private ImageLicense(string code, WebUrl? url)
    {
        Code = code;
        Url = url;
    }

    /// <summary>Gets the code, such as <c>CC BY 2.0</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the optional https link to the licence's text.</summary>
    public WebUrl? Url { get; }

    /// <summary>Creates a licence.</summary>
    /// <param name="code">The code; required, at most <see cref="MaxCodeLength"/> characters after sanitizing.</param>
    /// <param name="link">The optional https link to the licence's text.</param>
    /// <param name="field">The field of the licence; errors are on its <c>code</c> and <c>url</c>.</param>
    /// <returns>The licence, or validation errors that never echo the input.</returns>
    public static Result<ImageLicense> Create(string? code, string? link, string field = "license")
    {
        var errors = new ValidationErrors();
        var sanitized = SanitizedText.Required(errors, code, field + ".code", "License", MaxCodeLength);
        var validLink = CreditText.HttpsUrl(errors, link, field + ".url", "License link");
        return errors.HasErrors ? errors.ToResult<ImageLicense>() : Result.Ok(new ImageLicense(sanitized!, validLink));
    }

    /// <summary>For persistence mappers: rebuilds a licence stored earlier; it performs NO validation beyond required values, so never use it for input.</summary>
    /// <param name="code">The stored code.</param>
    /// <param name="link">The stored link, or <see langword="null"/>.</param>
    /// <returns>The licence.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is <see langword="null"/>, empty or blank.</exception>
    public static ImageLicense Restore(string code, string? link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new ImageLicense(code, string.IsNullOrWhiteSpace(link) ? null : WebUrl.Restore(link));
    }

    /// <summary>
    /// Gives the link of a common Creative Commons code: <c>CC BY</c>, <c>CC BY-SA</c>, <c>CC BY-ND</c>, <c>CC BY-NC</c>,
    /// <c>CC BY-NC-SA</c> and <c>CC BY-NC-ND</c> in versions 2.0, 2.5, 3.0 and 4.0 (<c>https://creativecommons.org/licenses/by-sa/4.0/</c>),
    /// <c>CC0 1.0</c> and <c>Public Domain Mark 1.0</c> (also written <c>PDM 1.0</c>). Case and repeated spaces are ignored.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The link, or <see langword="null"/> for any other code.</returns>
    public static WebUrl? KnownUrl(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var parts = code.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = string.Join(' ', parts).ToUpperInvariant();
        if (normalized == "CC0 1.0")
        {
            return WebUrl.Restore("https://creativecommons.org/publicdomain/zero/1.0/");
        }

        if (normalized is "PUBLIC DOMAIN MARK 1.0" or "PDM 1.0")
        {
            return WebUrl.Restore("https://creativecommons.org/publicdomain/mark/1.0/");
        }

#pragma warning disable CA1308 // Creative Commons licence paths are lower case.
        return parts is [var prefix, var elements, var version]
            && string.Equals(prefix, "CC", StringComparison.OrdinalIgnoreCase)
            && CreativeCommonsElements.Contains(elements.ToLowerInvariant())
            && CreativeCommonsVersions.Contains(version)
            ? WebUrl.Restore(string.Create(CultureInfo.InvariantCulture, $"https://creativecommons.org/licenses/{elements.ToLowerInvariant()}/{version}/"))
            : null;
#pragma warning restore CA1308
    }

    /// <inheritdoc />
    public override string ToString() => Code;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
        yield return Url;
    }
}
