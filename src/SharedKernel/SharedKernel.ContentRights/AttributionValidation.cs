using SharedKernel.BuildingBlocks;
using SharedKernel.InputNormalization;
using SharedKernel.Results;

namespace SharedKernel.ContentRights;

/// <summary>Validates public credit input while preserving supplied field values.</summary>
internal static class AttributionValidation
{
    /// <summary>Validates printable credit text.</summary>
    /// <param name="errors">The accumulator for errors.</param>
    /// <param name="value">The supplied text.</param>
    /// <param name="field">The key used in errors.</param>
    /// <param name="label">The name displayed in errors.</param>
    /// <param name="required">Whether a value must be supplied.</param>
    /// <returns>The supplied text if present and valid.</returns>
    public static string? Text(
        ValidationErrors errors, string? value, string field, string label, bool required = false)
    {
        if (value is { Length: > ContentAttribution.MaxTextLength })
        {
            Add(errors, field, $"{label} exceeds the text length limit.");
            return null;
        }

        if (value is not null && !string.Equals(PrintableText.OneLine(value), value, StringComparison.Ordinal))
        {
            Add(errors, field, $"{label} must contain printable text on one line.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (required)
        {
            Add(errors, field, $"{label} is required.");
        }

        return null;
    }

    /// <summary>Validates an optional absolute source or terms address.</summary>
    /// <param name="errors">The accumulator for errors.</param>
    /// <param name="value">The supplied address.</param>
    /// <param name="field">The key used in errors.</param>
    /// <param name="label">The name displayed in errors.</param>
    /// <returns>The validated address, if present.</returns>
    public static WebUrl? Url(ValidationErrors errors, string? value, string field, string label)
    {
        if (value is null)
        {
            return null;
        }

        var url = WebUrl.Create(value, field, label);
        errors.AddIfFailed(url);
        return url.TryGetValue(out var validated) ? validated : null;
    }

    /// <summary>Records an input-independent field error.</summary>
    /// <param name="errors">The accumulator for errors.</param>
    /// <param name="field">The key used in errors.</param>
    /// <param name="message">The validation message.</param>
    public static void Add(ValidationErrors errors, string field, string message)
        => errors.Add(Result.Invalid(detail: message, field: field, message: message));
}
