using System.Text.Json;

namespace PandaBear.Repository.Tests;

internal readonly record struct WarningReportView(int Blocking, int Advisory, int Accepted)
{
    public static WarningReportView Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new WarningReportView(root.GetProperty("blocking").GetInt32(), root.GetProperty("advisory").GetInt32(), root.GetProperty("accepted").GetInt32());
    }
}
