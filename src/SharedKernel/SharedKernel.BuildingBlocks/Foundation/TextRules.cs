using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>Shared validation for free-text value-object parts. Messages never echo the input.</summary>
internal static class TextRules
{
    /// <summary>Sanitizes a required part and records an error when it is empty or too long.</summary>
    internal static string Required(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;
        if (sanitized.Length == 0)
        {
            errors.Add(Result.Invalid(detail: $"{label} is required.", field: field, message: $"{label} is required."));
        }
        else if (sanitized.Length > maxLength)
        {
            errors.Add(Result.Invalid(detail: $"{label} cannot exceed {maxLength} characters.", field: field, message: $"{label} cannot exceed {maxLength} characters."));
        }

        return sanitized;
    }

    /// <summary>Sanitizes an optional part; blank becomes <see langword="null"/>. Records an error when too long.</summary>
    internal static string? Optional(ValidationErrors errors, string? value, string field, string label, int maxLength)
    {
        var sanitized = StringSanitizer.Sanitize(value);
        if (string.IsNullOrEmpty(sanitized))
        {
            return null;
        }

        if (sanitized.Length > maxLength)
        {
            errors.Add(Result.Invalid(detail: $"{label} cannot exceed {maxLength} characters.", field: field, message: $"{label} cannot exceed {maxLength} characters."));
        }

        return sanitized;
    }
}
