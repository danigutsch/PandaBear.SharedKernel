namespace PandaBear.Packages.Tests;

/// <summary>Canned OSV answers in the shape that api.osv.dev returned when the adapter was written.</summary>
internal static class OsvAnswers
{
    /// <summary>A batch answer with one result per entry; an entry is a list of vulnerability IDs, or null for <c>{}</c>.</summary>
    public static string BatchPaged(params (string[]? Ids, string? NextPageToken)[] results) =>
        "{\"results\":[" + string.Join(",", results.Select(static r => Result(r.Ids, r.NextPageToken))) + "]}";

    /// <summary>A batch answer with one result per entry and no paging.</summary>
    public static string Batch(params string[]?[] results) => BatchPaged([.. results.Select(static ids => (ids, (string?)null))]);

    /// <summary>A vulnerability record whose package is affected only through a range, like most GHSA records.</summary>
    public static string RangeOnly(string id, string package, string ecosystem = "npm", string? databaseSeverity = "HIGH") =>
        $$"""
        {"id":"{{id}}","summary":"x","database_specific":{{DatabaseSpecific(databaseSeverity)}},
         "affected":[{"package":{"name":"{{package}}","ecosystem":"{{ecosystem}}"},"ranges":[{"type":"SEMVER","events":[{"introduced":"0"},{"fixed":"9.9.9"}]}]}]}
        """;

    /// <summary>A vulnerability record that also lists exact versions.</summary>
    public static string WithVersions(string id, string package, string[] versions, string ecosystem = "npm") =>
        $$"""
        {"id":"{{id}}","database_specific":{"severity":"LOW"},
         "affected":[{"package":{"name":"{{package}}","ecosystem":"{{ecosystem}}"},"versions":[{{string.Join(",", versions.Select(static v => "\"" + v + "\""))}}]}]}
        """;

    private static string DatabaseSpecific(string? severity) => severity is null ? "{}" : $"{{\"severity\":\"{severity}\"}}";

    private static string Result(string[]? ids, string? token)
    {
        var parts = new List<string>();
        if (ids is not null)
        {
            parts.Add("\"vulns\":[" + string.Join(",", ids.Select(static id => $"{{\"id\":\"{id}\",\"modified\":\"2026-01-01T00:00:00Z\"}}")) + "]");
        }

        if (token is not null)
        {
            parts.Add($"\"next_page_token\":\"{token}\"");
        }

        return "{" + string.Join(",", parts) + "}";
    }
}
