namespace PandaBear.Results.Tests;

internal sealed class LoggedTourSummary(string code, string title)
{
    public override string ToString() => $"{code} | {title}";
}
