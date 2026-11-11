namespace PandaBear.DocumentRendering;

/// <summary>
/// Defines one block of prose displayed in a document section, such as a contract clause. The text is the caller's
/// final wording, including any numbering; the renderer adds no numbering and encodes the text.
/// </summary>
public sealed record DocumentParagraph(
    string Text,
    DocumentPrivacyClassification PrivacyClassification) : DocumentBlock;
