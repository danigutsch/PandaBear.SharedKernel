using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The attribution of a third-party image, following the Creative Commons practice of naming the title, the author, the source and the
/// licence, and noting when the image was changed: <see cref="Author"/>, <see cref="Title"/>, <see cref="SourceUrl"/>,
/// <see cref="License"/> and <see cref="Modified"/>. All copy, such as <c>Foto: {0}, {1}</c>, is the application's, and every format
/// takes the same placeholders: <c>{0}</c> the author, <c>{1}</c> the licence code, <c>{2}</c> the title and <c>{3}</c> the source
/// address, the last two empty when absent.
/// </summary>
public sealed class ImageCredit : ValueObject
{
    /// <summary>The longest author.</summary>
    public const int MaxAuthorLength = 200;

    /// <summary>The longest title.</summary>
    public const int MaxTitleLength = 200;

    private ImageCredit(string author, string? title, WebUrl? sourceUrl, ImageLicense license, bool modified)
    {
        Author = author;
        Title = title;
        SourceUrl = sourceUrl;
        License = license;
        Modified = modified;
    }

    /// <summary>Gets the author, a person or an account name.</summary>
    public string Author { get; }

    /// <summary>Gets the optional title of the work.</summary>
    public string? Title { get; }

    /// <summary>Gets the optional https address of the page the image comes from.</summary>
    public WebUrl? SourceUrl { get; }

    /// <summary>Gets the licence.</summary>
    public ImageLicense License { get; }

    /// <summary>Gets a value indicating whether the image was changed, such as cropped or resized.</summary>
    public bool Modified { get; }

    /// <summary>Creates a credit after validating every part.</summary>
    /// <param name="author">The author; required, at most <see cref="MaxAuthorLength"/> characters after sanitizing.</param>
    /// <param name="title">The optional title, at most <see cref="MaxTitleLength"/> characters.</param>
    /// <param name="source">The optional https address of the page the image comes from.</param>
    /// <param name="licenseCode">The licence code; required (see <see cref="ImageLicense"/>).</param>
    /// <param name="licenseLink">The optional https link to the licence; <see cref="ImageLicense.KnownUrl"/> gives the common ones.</param>
    /// <param name="modified">Whether the image was changed.</param>
    /// <param name="fieldPrefix">The path of the credit, such as <c>credit</c>; empty puts the errors on <c>author</c>, <c>title</c>,
    /// <c>sourceUrl</c>, <c>license.code</c> and <c>license.url</c> directly.</param>
    /// <returns>The credit, or every validation error; messages never echo the input.</returns>
    public static Result<ImageCredit> Create(
        string? author,
        string? title,
        string? source,
        string? licenseCode,
        string? licenseLink,
        bool modified,
        string fieldPrefix = "")
    {
        string Field(string name) => string.IsNullOrEmpty(fieldPrefix) ? name : fieldPrefix + "." + name;

        var errors = new ValidationErrors();
        var validAuthor = SanitizedText.Required(errors, author, Field("author"), "Author", MaxAuthorLength);
        var validTitle = SanitizedText.Optional(errors, title, Field("title"), "Title", MaxTitleLength);
        var validSource = CreditText.HttpsUrl(errors, source, Field("sourceUrl"), "Source link");
        var license = ImageLicense.Create(licenseCode, licenseLink, Field("license"));
        errors.AddIfFailed(license);
        return errors.HasErrors
            ? errors.ToResult<ImageCredit>()
            : Result.Ok(new ImageCredit(validAuthor!, validTitle, validSource, license.Value, modified));
    }

    /// <summary>For persistence mappers: rebuilds a credit stored earlier; it performs NO validation beyond required values, so never use it for input.</summary>
    /// <param name="author">The stored author.</param>
    /// <param name="title">The stored title.</param>
    /// <param name="source">The stored source address.</param>
    /// <param name="license">The stored licence, from <see cref="ImageLicense.Restore"/>.</param>
    /// <param name="modified">Whether the image was changed.</param>
    /// <returns>The credit.</returns>
    /// <exception cref="ArgumentException"><paramref name="author"/> is <see langword="null"/>, empty or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is <see langword="null"/>.</exception>
    public static ImageCredit Restore(string author, string? title, string? source, ImageLicense license, bool modified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentNullException.ThrowIfNull(license);

        return new ImageCredit(
            author,
            string.IsNullOrWhiteSpace(title) ? null : title,
            string.IsNullOrWhiteSpace(source) ? null : WebUrl.Restore(source),
            license,
            modified);
    }

    /// <summary>Builds the short credit shown with the image, such as <c>Foto: Ana Silva, CC BY 2.0</c>.</summary>
    /// <param name="format">The application's format: <c>{0}</c> the author, <c>{1}</c> the licence code, <c>{2}</c> the title and <c>{3}</c> the source address, each empty when absent.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException"><paramref name="format"/> is <see langword="null"/>, empty or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public string ToShortText(string format, IFormatProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(provider);

        return string.Format(provider, format, Author, License.Code, Title ?? string.Empty, SourceUrl?.Value ?? string.Empty);
    }

    /// <summary>
    /// Builds the full credit for a credits page: the title format when the credit has a title, else the untitled one, then the change
    /// note when the image was changed. Links are the display's: it can link <see cref="SourceUrl"/> and <see cref="ImageLicense.Url"/>.
    /// </summary>
    /// <param name="formats">The application's formats.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public string ToFullText(ImageCreditFormats formats, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(provider);

        var text = string.Format(provider, Title is null ? formats.WithoutTitle : formats.WithTitle, Author, License.Code, Title ?? string.Empty, SourceUrl?.Value ?? string.Empty);
        return Modified ? string.Format(provider, formats.Modified, text) : text;
    }

    /// <inheritdoc />
    public override string ToString() => Title is null ? Author : Title + " / " + Author;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Author;
        yield return Title;
        yield return SourceUrl;
        yield return License;
        yield return Modified;
    }
}
