using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// The test method name segments that SKTEST002 accepts outside the sentence-style lowercase form.
/// </summary>
/// <remarks>
/// <para>
/// A built-in entry is a widely used acronym, protocol or file format, or a public .NET, ASP.NET Core or test framework
/// type name that test names refer to across repositories. Names that exist in one repository only belong in that
/// repository's <c>sharedkernel_testing_additional_allowed_segments</c> setting, not here. Each concept has one spelling:
/// acronyms are upper case (<c>JSON</c>, <c>GUID</c>) and type names keep their declared casing (<c>DbContext</c>).
/// </para>
/// <para>
/// A segment matches an entry when it equals the entry ignoring case, as a whole segment between underscores, never as
/// a substring. A matching segment must use the entry's spelling, so <c>ui</c> must be written <c>UI</c>, unless the
/// entry is a capitalised plain word such as <c>Task</c>: its lowercase form is an ordinary English word and stays
/// allowed.
/// </para>
/// </remarks>
internal static class TestMethodNameSegments
{
    /// <summary>The SKTEST002 diagnostic property that carries the configured entries, comma-separated, to the code fix.</summary>
    internal const string AdditionalSegmentsProperty = "AdditionalAllowedSegments";

    private const string DiagnosticIdPrefix = "SKTEST";

    /// <summary>The built-in entries, always on in every repository.</summary>
    internal static readonly ImmutableArray<string> BuiltIn = [
        "AI",
        "API",
        "AsyncAPI",
        "CSP",
        "CSS",
        "CSV",
        "CORS",
        "CancellationToken",
        "ClaimsPrincipal",
        "DTO",
        "DataAnnotationsValidator",
        "DateOnly",
        "DateTime",
        "DbContext",
        "EF",
        "ETag",
        "EditContext",
        "GUID",
        "HTML",
        "HTTP",
        "HTTP2",
        "HttpClient",
        "HttpContext",
        "HttpRequest",
        "HttpResponse",
        "HttpStatusCode",
        "ID",
        "IDs",
        "IQueryable",
        "JSON",
        "JWT",
        "NavLink",
        "NavLinks",
        "NavMenu",
        "OAuth",
        "OIDC",
        "OpenApi",
        "PDF",
        "ProblemDetails",
        "QuickGrid",
        "SKTEST",
        "SQL",
        "Task",
        "TimeOnly",
        "TimeSpan",
        "URI",
        "URL",
        "UI",
        "UTC",
        "ValidationMessage",
        "ValueTask",
        "XML",
        "xUnit",
    ];

    /// <summary>Returns the listed spelling of a segment that matches a built-in entry, or null.</summary>
    /// <param name="segment">One underscore-separated segment of a test method name.</param>
    public static string? Canonical(string segment)
    {
        return Canonical(segment, []);
    }

    /// <summary>Returns the listed spelling of a segment that matches a built-in or configured entry, or null.</summary>
    /// <param name="segment">One underscore-separated segment of a test method name.</param>
    /// <param name="additional">Entries configured by the repository.</param>
    public static string? Canonical(string segment, ImmutableArray<string> additional)
    {
        if (segment is null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        var exact = Find(segment, additional, StringComparison.Ordinal);
        if (exact is not null)
        {
            return exact;
        }

        if (segment.StartsWith(DiagnosticIdPrefix, StringComparison.OrdinalIgnoreCase)
            && segment.Length > DiagnosticIdPrefix.Length
            && segment.Substring(DiagnosticIdPrefix.Length).All(char.IsDigit))
        {
            return DiagnosticIdPrefix + segment.Substring(DiagnosticIdPrefix.Length);
        }

        return Find(segment, additional, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reports whether an entry must always be written in its listed spelling.</summary>
    /// <param name="entry">A built-in or configured entry.</param>
    /// <returns>False for a capitalised plain word such as <c>Task</c>, whose lowercase form stays allowed; true for acronyms such as <c>S3</c>.</returns>
    public static bool ForcesCasing(string entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        var isCapitalisedWord = entry.Length > 1
            && char.IsUpper(entry[0])
            && entry.Skip(1).Any(char.IsLower)
            && entry.Skip(1).All(static character => char.IsLower(character) || char.IsDigit(character));
        return !isCapitalisedWord;
    }

    /// <summary>Reports whether an entry names the same segment as a built-in entry, ignoring case.</summary>
    /// <param name="entry">A configured entry.</param>
    public static bool IsBuiltIn(string entry)
    {
        return Find(entry, [], StringComparison.OrdinalIgnoreCase) is not null;
    }

    /// <summary>Reports whether a segment is an accepted spelling of the entry it matches.</summary>
    /// <param name="segment">One underscore-separated segment of a test method name.</param>
    /// <param name="canonical">The entry the segment matches, as returned by <see cref="Canonical(string, ImmutableArray{string})"/>.</param>
    public static bool IsAcceptedSpelling(string segment, string canonical)
    {
        if (segment is null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        return string.Equals(segment, canonical, StringComparison.Ordinal)
            || (!ForcesCasing(canonical) && segment.All(static character => char.IsLower(character) || char.IsDigit(character)));
    }

    /// <summary>Reports whether an entry has a name boundary inside it, such as <c>DbContext</c>, so a name splitter must keep it whole.</summary>
    /// <param name="entry">A built-in or configured entry.</param>
    public static bool IsCompound(string entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        return entry.Any(char.IsLower) && entry.Skip(1).Any(char.IsUpper);
    }

    private static string? Find(string segment, ImmutableArray<string> additional, StringComparison comparison)
    {
        foreach (var entry in BuiltIn)
        {
            if (string.Equals(segment, entry, comparison))
            {
                return entry;
            }
        }

        if (additional.IsDefault)
        {
            return null;
        }

        foreach (var entry in additional)
        {
            if (string.Equals(segment, entry, comparison))
            {
                return entry;
            }
        }

        return null;
    }
}
