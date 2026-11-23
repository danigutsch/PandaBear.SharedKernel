using System.Globalization;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.PageSections;

/// <summary>Plain-text rules of section content: sanitized, length-capped, never echoed in a message.</summary>
internal static class SectionText
{
    /// <summary>Sanitizes required one-line text.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxLength">The longest text.</param>
    /// <returns>The text, empty when refused.</returns>
    public static string Required(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            Add(errors, field, $"{label} is not valid text.");
            return string.Empty;
        }

        if (string.IsNullOrEmpty(sanitized))
        {
            Add(errors, field, $"{label} is required.");
            return string.Empty;
        }

        return Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Sanitizes optional one-line text; blank is <see langword="null"/>.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxLength">The longest text.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string? Optional(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            Add(errors, field, $"{label} is not valid text.");
            return null;
        }

        return string.IsNullOrEmpty(sanitized) ? null : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Splits text into paragraphs on blank lines.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="rules">The rules.</param>
    /// <returns>The paragraphs.</returns>
    public static IReadOnlyList<string> Paragraphs(ValidationErrors errors, string? value, string field, string label, PageSectionRules rules)
    {
        if (!StringSanitizer.TrySanitizeParagraphs(value, out var paragraphs))
        {
            Add(errors, field, $"{label} is not valid text.");
        }
        else if (paragraphs.Count > rules.MaxParagraphs)
        {
            Add(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot have more than {rules.MaxParagraphs} paragraphs."));
        }
        else if (paragraphs.Any(paragraph => paragraph.Length > rules.MaxParagraphLength))
        {
            Add(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label}: each paragraph must be at most {rules.MaxParagraphLength} characters."));
        }

        return paragraphs;
    }

    /// <summary>Records an error.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="field">The field path.</param>
    /// <param name="message">The message, which never echoes input.</param>
    public static void Add(ValidationErrors errors, string field, string message) =>
        errors.Add(Result.Invalid(detail: message, field: field, message: message));

    private static string Capped(ValidationErrors errors, string sanitized, string field, string label, int maxLength)
    {
        if (sanitized.Length > maxLength)
        {
            Add(errors, field, string.Create(CultureInfo.InvariantCulture, $"{label} cannot exceed {maxLength} characters."));
        }

        return sanitized;
    }
}
