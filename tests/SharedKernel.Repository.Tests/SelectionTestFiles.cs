using System.Text.Json;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Answers the Git commands of <c>select</c> and reads the plan files it writes.</summary>
internal static class SelectionTestFiles
{
    /// <summary>Answers <c>git diff</c> with <paramref name="diff"/>, <c>git ls-files</c> with <paramref name="files"/>, and <c>git rev-parse</c> with <c>fixture</c>.</summary>
    public static Func<string, string[], CancellationToken, Task<string>> Git(string diff, string files = "") =>
        (_, arguments, _) => Task.FromResult(arguments[0] switch
        {
            "diff" => diff,
            "ls-files" => files,
            "rev-parse" => "fixture",
            _ => string.Empty,
        });

    /// <summary>Answers the comparison with a change of <paramref name="path"/> and runs every other Git command in the checkout.</summary>
    public static Func<string, string[], string, CancellationToken, Task<string>> CheckoutGit(string path) =>
        (fileName, arguments, directory, ct) => arguments[0] switch
        {
            "merge-base" => Task.FromResult(string.Empty),
            "diff" => Task.FromResult(path + "\0"),
            _ => ProcessRunner.Run(fileName, arguments, directory, ct),
        };

    public static async Task<SelectionPlan> ReadPlan(string selection, CancellationToken ct) =>
        JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(selection, "selection.json"), ct), SelectionPlanJsonContext.Default.SelectionPlan)!;

    /// <summary>Returns the projects that a filter in the plan directory lists.</summary>
    public static async Task<string[]> Projects(string selection, string filter, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(selection, filter + ".slnf"), ct));
        return [.. document.RootElement.GetProperty("solution").GetProperty("projects").EnumerateArray().Select(static project => project.GetString()!)];
    }

    /// <summary>Returns the selected test projects of a slice.</summary>
    public static string[] Tests(SelectionPlan selection, string slice) =>
        [.. selection.Slices.Single(candidate => candidate.Name == slice).Tests.Select(static test => test.Project)];
}
