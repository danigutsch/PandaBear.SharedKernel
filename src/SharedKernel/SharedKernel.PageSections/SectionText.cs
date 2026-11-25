using SharedKernel.Results;

namespace SharedKernel.PageSections;

/// <summary>Records the errors of section content; messages never echo the input.</summary>
internal static class SectionText
{
    /// <summary>Records an error.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="field">The field path.</param>
    /// <param name="message">The message, which never echoes input.</param>
    public static void Add(ValidationErrors errors, string field, string message) =>
        errors.Add(Result.Invalid(detail: message, field: field, message: message));
}
