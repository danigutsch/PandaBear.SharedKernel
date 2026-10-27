namespace PandaBear.Blazor;

/// <summary>One entry of an <see cref="ErrorSummary" />.</summary>
/// <param name="TargetId">The HTML id of the form control the entry links to, without a leading hash.</param>
/// <param name="Message">The message shown as the link text.</param>
public sealed record ErrorSummaryItem(string TargetId, string Message);
