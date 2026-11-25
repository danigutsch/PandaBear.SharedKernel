namespace PandaBear.DocumentRendering;

/// <summary>
/// Defines a standalone link displayed in a document section.
/// </summary>
/// <param name="Text">The meaningful link text displayed in the document.</param>
/// <param name="Target">An absolute HTTPS destination.</param>
/// <param name="PrivacyClassification">The required classification of the link text and destination.</param>
public sealed record DocumentLink(
    string Text,
    Uri Target,
    DocumentPrivacyClassification PrivacyClassification) : DocumentBlock;
