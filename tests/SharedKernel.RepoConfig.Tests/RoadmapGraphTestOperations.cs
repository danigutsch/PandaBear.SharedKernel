using System.Reflection;

namespace PandaBear.RepoConfig.Tests;

internal static class RoadmapGraphTestOperations
{
    public static RoadmapItemSnapshot Item(string id, IReadOnlyList<string> blockers, string? parent = null) =>
        new(id, id, id, "issue", "open", "theme", false, null, parent, null, null, null, null,
            blockers, [], [], [], [], null, false);

    public static IReadOnlyList<RepoConfigIssue> Verify(RoadmapItemSnapshot[] items, string methodName)
    {
        var method = typeof(RepoConfigVerifier).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();
        List<RepoConfigIssue> issues = [];
        method.Invoke(null, [items, issues]);
        return issues;
    }
}
