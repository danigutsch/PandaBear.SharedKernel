using System.Globalization;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.Offerings;

/// <summary>Plain-text rules of offering content: sanitized, length-capped, never echoed in a message.</summary>
internal static class TextInput
{
    /// <summary>Sanitizes required one-line text.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxLength">The longest text.</param>
    /// <returns>The text, or <see langword="null"/> when it is refused.</returns>
    public static string? Required(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        return string.IsNullOrEmpty(sanitized)
            ? Refuse(errors, field, $"{label} is required.")
            : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Sanitizes optional one-line text; blank is <see langword="null"/>.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxLength">The longest text.</param>
    /// <returns>The text, or <see langword="null"/> when it is blank or refused.</returns>
    public static string? Optional(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        return string.IsNullOrEmpty(sanitized) ? null : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Splits text into sanitized paragraphs on blank lines.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxParagraphs">The most paragraphs.</param>
    /// <param name="maxLength">The longest paragraph.</param>
    /// <returns>The paragraphs, possibly none.</returns>
    public static IReadOnlyList<string> Paragraphs(ValidationErrors errors, string? value, string field, string label, int maxParagraphs, int maxLength)
    {
        if (!StringSanitizer.TrySanitizeParagraphs(value, out var paragraphs))
        {
            Refuse(errors, field, $"{label} is not valid text.");
        }
        else if (paragraphs.Count > maxParagraphs)
        {
            Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot have more than {maxParagraphs} paragraphs."));
        }
        else if (paragraphs.Any(paragraph => paragraph.Length > maxLength))
        {
            Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label}: each paragraph must be at most {maxLength} characters."));
        }

        return paragraphs;
    }

    /// <summary>Sanitizes a list of required one-line items; one error on the list names the first broken rule.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="values">The items; <see langword="null"/> is none.</param>
    /// <param name="field">The field path of the list.</param>
    /// <param name="label">The label of the list in messages.</param>
    /// <param name="maxItems">The most items.</param>
    /// <param name="maxLength">The longest item.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<string> Lines(ValidationErrors errors, IReadOnlyList<string?>? values, string field, string label, int maxItems, int maxLength)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        if (values.Count > maxItems)
        {
            Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot have more than {maxItems} items."));
            return [];
        }

        var items = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!StringSanitizer.TrySanitize(value, out var sanitized))
            {
                Refuse(errors, field, $"{label} is not valid text.");
                return [];
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                Refuse(errors, field, $"{label} cannot have an empty item.");
                return [];
            }

            if (sanitized.Length > maxLength)
            {
                Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label}: each item must be at most {maxLength} characters."));
                return [];
            }

            items.Add(sanitized);
        }

        return items.AsReadOnly();
    }

    /// <summary>Records an error.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="field">The field path.</param>
    /// <param name="message">The message, which never echoes input.</param>
    /// <returns><see langword="false"/>, so a caller can mark the value invalid.</returns>
    public static bool Add(ValidationErrors errors, string field, string message)
    {
        errors.Add(Result.Invalid(detail: message, field: field, message: message));
        return false;
    }

    private static string? Capped(ValidationErrors errors, string sanitized, string field, string label, int maxLength) =>
        sanitized.Length > maxLength
            ? Refuse(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot exceed {maxLength} characters."))
            : sanitized;

    private static string? Refuse(ValidationErrors errors, string field, string message)
    {
        Add(errors, field, message);
        return null;
    }
}
