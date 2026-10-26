namespace PandaBear.Repository.Tests;

internal static class WarningToolRuns
{
    public static string Ledger(string rule, string scope, int baseline, string expires) =>
        $$"""
        {"exceptions":[{"tool":"NuGet restore","rule":"{{rule}}","scope":"{{scope}}","baseline":{{baseline}},"rationale":"Upstream fix pending.","owner":"Dependency owners","expires":"{{expires}}"}]}
        """;

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

    public static async Task<(RepositoryToolResult Result, string? Report)> Produce(string[] arguments, CancellationToken ct)
    {
        using var scope = new RepositoryTestScope();
        var result = await RepositoryToolInvocation.Run(scope.Root, ["warning-report", .. arguments, "--report", "report.json"], new ProcessLog().Run, ct);
        var path = Path.Combine(scope.Root, "report.json");
        return (result, File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null);
    }
}
