namespace PandaBear.DocumentRendering;

/// <summary>
/// Defines a real list displayed in a document section, such as the services a contract includes. Each item is the
/// caller's final wording and is encoded by the renderer, which adds no bullets or numbers of its own: the list markers
/// come from the HTML list element, so assistive technology and Word see a list rather than lines that start with a symbol.
/// </summary>
public sealed record DocumentList(
    IReadOnlyList<string> Items,
    DocumentPrivacyClassification PrivacyClassification)
{
    /// <summary>
    /// Gets a value indicating whether the list is ordered (numbered). It is unordered (bulleted) by default.
    /// </summary>
    public bool IsOrdered
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the optional accessible name of the list, written as its <c>aria-label</c>. It is not displayed. It is
    /// omitted when null or blank.
    /// </summary>
    public string? Label
    {
        get;
        init;
    }
}
