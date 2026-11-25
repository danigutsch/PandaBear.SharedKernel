using Microsoft.AspNetCore.Components.Forms;

namespace SharedKernel.Blazor;

/// <summary>Builds field-linked error summaries from an edit context.</summary>
public static class EditContextErrorSummary
{
    /// <summary>Lists messages in the supplied field order, preserving message order within each field.</summary>
    /// <param name="context">The edit context containing the validation messages.</param>
    /// <param name="fields">The field identifiers and their corresponding focus target IDs.</param>
    /// <returns>The current messages for the supplied fields.</returns>
    /// <exception cref="ArgumentNullException">The context or fields are null.</exception>
    public static IReadOnlyList<ErrorSummaryItem> Build(EditContext context, IEnumerable<(FieldIdentifier Field, string TargetId)> fields)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fields);
        return [.. fields.SelectMany(field => context.GetValidationMessages(field.Field)
            .Select(message => new ErrorSummaryItem(field.TargetId, message)))];
    }
}
