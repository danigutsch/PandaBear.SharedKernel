using System.Globalization;
using SharedKernel.InputNormalization;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Validation of plain text a person entered: each value is sanitized with <see cref="StringSanitizer"/>, malformed UTF-16 is refused,
/// and the length is capped. Each method records its errors on one field and returns nothing (<see langword="null"/> or an empty list)
/// when it records one, so a refused value never reaches the result. Messages start with the label and never echo the input.
/// </summary>
public static class SanitizedText
{
    /// <summary>Sanitizes required one-line text.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <param name="label">The label that starts the messages, such as <c>Title</c>.</param>
    /// <param name="maxLength">The longest text after sanitizing.</param>
    /// <returns>The sanitized text, or <see langword="null"/> when it is malformed (<c>{label} is not valid text.</c>), blank
    /// (<c>{label} is required.</c>) or longer than <paramref name="maxLength"/> (<c>{label} cannot exceed {maxLength} characters.</c>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static string? Required(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        return string.IsNullOrEmpty(sanitized)
            ? Refuse(errors, field, $"{label} is required.")
            : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Sanitizes optional one-line text; blank text is <see langword="null"/> without an error.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <param name="label">The label that starts the messages.</param>
    /// <param name="maxLength">The longest text after sanitizing.</param>
    /// <returns>The sanitized text, or <see langword="null"/> when it is blank, malformed (<c>{label} is not valid text.</c>) or longer
    /// than <paramref name="maxLength"/> (<c>{label} cannot exceed {maxLength} characters.</c>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static string? Optional(ValidationErrors errors, string? value, string field, string label, int maxLength) =>
        Optional(errors, value, field, label, maxLength, out _);

    /// <summary>
    /// Sanitizes optional one-line text like <see cref="Optional(ValidationErrors, string?, string, string, int)"/>, and tells whether
    /// any was entered, so a rule that needs the text does not also report it as missing when it is only refused.
    /// </summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <param name="label">The label that starts the messages.</param>
    /// <param name="maxLength">The longest text after sanitizing.</param>
    /// <param name="entered"><see langword="true"/> when the text is not blank after sanitizing, or is malformed; it is then present
    /// even when it is refused.</param>
    /// <returns>The sanitized text, or <see langword="null"/> when it is blank or refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static string? Optional(ValidationErrors errors, string? value, string field, string label, int maxLength, out bool entered)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            entered = true;
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        entered = !string.IsNullOrEmpty(sanitized);
        return entered ? Capped(errors, sanitized!, field, label, maxLength) : null;
    }

    /// <summary>
    /// Splits text into sanitized paragraphs on blank lines, as <see cref="StringSanitizer.TrySanitizeParagraphs"/> does, and records one
    /// error for the first broken rule: <c>{label} is not valid text.</c>, <c>{label} cannot have more than {maxParagraphs} paragraphs.</c>
    /// or <c>{label}: each paragraph must be at most {maxLength} characters.</c>
    /// </summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <param name="label">The label that starts the messages.</param>
    /// <param name="maxParagraphs">The most paragraphs.</param>
    /// <param name="maxLength">The longest paragraph.</param>
    /// <returns>The paragraphs; empty when the text is blank or refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Paragraphs(ValidationErrors errors, string? value, string field, string label, int maxParagraphs, int maxLength) =>
        Paragraphs(errors, value, field, label, maxParagraphs, maxLength, out _);

    /// <summary>
    /// Splits text into sanitized paragraphs like <see cref="Paragraphs(ValidationErrors, string?, string, string, int, int)"/>, and
    /// tells whether any text was entered, so a rule that needs the text does not also report it as missing when it is only refused.
    /// </summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <param name="label">The label that starts the messages.</param>
    /// <param name="maxParagraphs">The most paragraphs.</param>
    /// <param name="maxLength">The longest paragraph.</param>
    /// <param name="entered"><see langword="true"/> when the text holds a paragraph, or is malformed; it is then present even when it is
    /// refused.</param>
    /// <returns>The paragraphs; empty when the text is blank or refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Paragraphs(
        ValidationErrors errors,
        string? value,
        string field,
        string label,
        int maxParagraphs,
        int maxLength,
        out bool entered)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!StringSanitizer.TrySanitizeParagraphs(value, out var paragraphs))
        {
            entered = true;
            return RefuseAll(errors, field, $"{label} is not valid text.");
        }

        entered = paragraphs.Count > 0;
        if (paragraphs.Count > maxParagraphs)
        {
            return RefuseAll(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot have more than {maxParagraphs} paragraphs."));
        }

        return paragraphs.Any(paragraph => paragraph.Length > maxLength)
            ? RefuseAll(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label}: each paragraph must be at most {maxLength} characters."))
            : paragraphs;
    }

    /// <summary>
    /// Sanitizes a list of required one-line items and records one error on the list for the first broken rule:
    /// <c>{label} cannot have more than {maxItems} items.</c>, <c>{label} is not valid text.</c>, <c>{label} cannot have an empty item.</c>
    /// or <c>{label}: each item must be at most {maxLength} characters.</c>
    /// </summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="values">The items; <see langword="null"/> is none.</param>
    /// <param name="field">The field path of the list.</param>
    /// <param name="label">The label of the list that starts the messages.</param>
    /// <param name="maxItems">The most items.</param>
    /// <param name="maxLength">The longest item.</param>
    /// <returns>The sanitized items; empty when there are none or the list is refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Lines(ValidationErrors errors, IReadOnlyList<string?>? values, string field, string label, int maxItems, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (values is null || values.Count == 0)
        {
            return [];
        }

        if (values.Count > maxItems)
        {
            return RefuseAll(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot have more than {maxItems} items."));
        }

        var items = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!StringSanitizer.TrySanitize(value, out var sanitized))
            {
                return RefuseAll(errors, field, $"{label} is not valid text.");
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                return RefuseAll(errors, field, $"{label} cannot have an empty item.");
            }

            if (sanitized.Length > maxLength)
            {
                return RefuseAll(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label}: each item must be at most {maxLength} characters."));
            }

            items.Add(sanitized);
        }

        return items.AsReadOnly();
    }

    private static string? Capped(ValidationErrors errors, string sanitized, string field, string label, int maxLength) =>
        sanitized.Length > maxLength
            ? Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot exceed {maxLength} characters."))
            : sanitized;

    private static string? Refuse(ValidationErrors errors, string field, string message)
    {
        errors.Add(Result.Invalid(detail: message, field: field, message: message));
        return null;
    }

    private static IReadOnlyList<string> RefuseAll(ValidationErrors errors, string field, string message)
    {
        errors.Add(Result.Invalid(detail: message, field: field, message: message));
        return [];
    }
}
