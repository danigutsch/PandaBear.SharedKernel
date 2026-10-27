using System.Text.Json;

namespace PandaBear.Repository.Tests;

/// <summary>The three finding counts of a warning report file, for comparison in tests.</summary>
/// <param name="Blocking">The number of findings that fail the check.</param>
/// <param name="Advisory">The number of findings that are reported but do not fail the check.</param>
/// <param name="Accepted">The number of findings that an unexpired exception absorbed.</param>
internal readonly record struct WarningReportView(int Blocking, int Advisory, int Accepted)
{
    /// <summary>Reads the <c>blocking</c>, <c>advisory</c>, and <c>accepted</c> counts from the report JSON in <paramref name="json"/>.</summary>
    public static WarningReportView Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new WarningReportView(root.GetProperty("blocking").GetInt32(), root.GetProperty("advisory").GetInt32(), root.GetProperty("accepted").GetInt32());
    }
}
