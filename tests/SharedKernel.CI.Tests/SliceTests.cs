namespace PandaBear.CI.Tests;

/// <summary>Reads the selected test projects of a slice from a plan.</summary>
internal static class SliceTests
{
    /// <summary>Returns the selected test projects of <paramref name="slice"/>.</summary>
    public static string[] Of(SelectionPlan selection, string slice) =>
        [.. selection.Slices.Single(candidate => candidate.Name == slice).Tests.Select(static test => test.Project)];
}
