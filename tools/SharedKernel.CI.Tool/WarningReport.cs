namespace PandaBear.CI.Tool;

/// <summary>
/// The warning totals of one CI tool, written by a verification step and read by <c>warning-summary</c>. Any step can write
/// this JSON object, so a new tool joins the summary without a new parser.
/// </summary>
/// <param name="Tool">Names the producing tool, such as <c>NuGet restore</c>.</param>
/// <param name="Blocking">Counts the findings that fail the required check.</param>
/// <param name="Advisory">Counts the findings that stay visible without failing.</param>
/// <param name="Accepted">Counts the findings covered by a reviewed exception or baseline.</param>
/// <param name="BlockingFindings">Names each blocking finding with a stable rule and subject.</param>
internal sealed record WarningReport(string Tool, int Blocking, int Advisory, int Accepted, IReadOnlyList<string> BlockingFindings);
