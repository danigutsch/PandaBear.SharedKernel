using System.Globalization;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>Text rules of image credits: sanitized, length-capped, never echoed in a message, and https links.</summary>
internal static class CreditText
{
    /// <summary>Sanitizes required text.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The text.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <param name="maxLength">The longest text.</param>
    /// <returns>The text, or <see langword="null"/> when refused.</returns>
    public static string? Required(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        if (!StringSanitizer.TrySanitize(value, out var sanitized))
        {
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        return string.IsNullOrEmpty(sanitized) ? Refuse(errors, field, $"{label} is required.") : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Sanitizes optional text; blank is <see langword="null"/>.</summary>
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
            return Refuse(errors, field, $"{label} is not valid text.");
        }

        return string.IsNullOrEmpty(sanitized) ? null : Capped(errors, sanitized, field, label, maxLength);
    }

    /// <summary>Validates an optional https link; blank is <see langword="null"/>.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The link.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    public static WebUrl? HttpsUrl(ValidationErrors errors, string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var url = WebUrl.CreateHttps(value, field, label);
        errors.AddIfFailed(url);
        return url.IsSuccess ? url.Value : null;
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
}
