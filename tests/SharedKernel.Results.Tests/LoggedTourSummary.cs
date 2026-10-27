namespace PandaBear.Results.Tests;

/// <summary>
/// Tour summary test type whose string form is the code and title joined by a pipe, used to check what gets logged.
/// </summary>
/// <param name="code">The tour code printed first.</param>
/// <param name="title">The tour title printed after the code.</param>
internal sealed class LoggedTourSummary(string code, string title)
{
    public override string ToString() => $"{code} | {title}";
}
