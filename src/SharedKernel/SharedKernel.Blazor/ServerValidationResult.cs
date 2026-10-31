namespace PandaBear.Blazor;

/// <summary>The outcome of applying server validation errors to an edit context.</summary>
public sealed class ServerValidationResult
{
    /// <summary>Initializes a result.</summary>
    /// <param name="issues">The placed issues.</param>
    /// <param name="hasUnplacedErrors">Whether any error could not be placed.</param>
    internal ServerValidationResult(IReadOnlyList<ServerValidationIssue> issues, bool hasUnplacedErrors)
    {
        Issues = issues;
        HasUnplacedErrors = hasUnplacedErrors;
    }

    /// <summary>Gets the messages that were placed on known fields.</summary>
    public IReadOnlyList<ServerValidationIssue> Issues
    {
        get;
    }

    /// <summary>
    /// Gets whether the server reported errors that could not be shown on a known field: model-level
    /// or unknown keys, keys beyond the field cap, and messages beyond the per-field cap. Show caller-chosen fixed copy for them; never show the server text.
    /// </summary>
    public bool HasUnplacedErrors
    {
        get;
    }

    /// <summary>Creates error summary entries that link each issue to a form control.</summary>
    /// <param name="fieldId">Maps a known field name to the HTML id of its control.</param>
    /// <returns>One entry per issue.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fieldId"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ErrorSummaryItem> ToSummaryItems(Func<string, string> fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);

        return [.. Issues.Select(issue => new ErrorSummaryItem(fieldId(issue.FieldName), issue.Message))];
    }
}
