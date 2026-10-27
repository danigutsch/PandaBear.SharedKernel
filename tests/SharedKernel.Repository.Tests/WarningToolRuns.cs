namespace PandaBear.Repository.Tests;

/// <summary>Runs the warning commands of the CI tool in a temporary repository and returns their results and reports.</summary>
internal static class WarningToolRuns
{
    /// <summary>Returns an exception ledger JSON with one NuGet restore exception for <paramref name="rule"/> and <paramref name="scope"/>, allowing <paramref name="baseline"/> findings until <paramref name="expires"/>.</summary>
    public static string Ledger(string rule, string scope, int baseline, string expires) =>
        $$"""
        {"exceptions":[{"tool":"NuGet restore","rule":"{{rule}}","scope":"{{scope}}","baseline":{{baseline}},"rationale":"Upstream fix pending.","owner":"Dependency owners","expires":"{{expires}}"}]}
        """;

    /// <summary>Runs <c>verify-restore-warnings</c> on <paramref name="log"/>, with <paramref name="ledger"/> as the exceptions file when it is not null, and returns the result and the counts of the report file, or a default view when none was written.</summary>
    public static async Task<(RepositoryToolResult Result, WarningReportView Report)> Verify(string log, string? ledger, CancellationToken ct)
    {
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), log, ct);
        List<string> args = ["verify-restore-warnings", "--log", "restore.log", "--report", "report.json"];
        if (ledger is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(scope.Root, "exceptions.json"), ledger, ct);
            args.AddRange(["--exceptions", "exceptions.json"]);
        }

        var result = await RepositoryToolInvocation.Run(scope.Root, args, new ProcessLog().Run, ct);
        var report = File.Exists(Path.Combine(scope.Root, "report.json"))
            ? WarningReportView.Parse(await File.ReadAllTextAsync(Path.Combine(scope.Root, "report.json"), ct))
            : default;
        return (result, report);
    }

    /// <summary>Runs <c>warning-report</c> with <paramref name="arguments"/> and returns the result and the text of the report file, or null when none was written.</summary>
    public static async Task<(RepositoryToolResult Result, string? Report)> Produce(string[] arguments, CancellationToken ct)
    {
        using var scope = new RepositoryTestScope();
        var result = await RepositoryToolInvocation.Run(scope.Root, ["warning-report", .. arguments, "--report", "report.json"], new ProcessLog().Run, ct);
        var path = Path.Combine(scope.Root, "report.json");
        return (result, File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null);
    }
}
