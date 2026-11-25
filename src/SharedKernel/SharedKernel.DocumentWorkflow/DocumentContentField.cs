using SharedKernel.DocumentRendering;

namespace SharedKernel.DocumentWorkflow;

/// <summary>Represents a stable, classified field in a draft.</summary>
/// <param name="Id">The field identifier within the draft.</param>
/// <param name="Label">The display label of the field.</param>
/// <param name="Value">The resolved field value.</param>
/// <param name="PrivacyClassification">The application's classification of the value.</param>
/// <param name="IsEditable">Whether a human can edit the value.</param>
public sealed record DocumentContentField(
    string Id,
    string Label,
    string Value,
    DocumentPrivacyClassification PrivacyClassification,
    bool IsEditable)
{
    /// <summary>Returns a diagnostic representation that omits the field payload.</summary>
    /// <returns>The redacted representation.</returns>
    public override string ToString() => $"{nameof(DocumentContentField)} [redacted]";
}
