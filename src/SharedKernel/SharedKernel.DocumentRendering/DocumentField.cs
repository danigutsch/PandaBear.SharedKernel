namespace SharedKernel.DocumentRendering;

/// <summary>
/// Defines one structured value displayed in a document section.
/// </summary>
public sealed record DocumentField(
    string Label,
    string Value,
    DocumentPrivacyClassification PrivacyClassification);
