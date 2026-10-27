using System.Reflection;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Builds roadmap item snapshots and invokes the verifier's private graph checks by name.</summary>
internal static class RoadmapGraphTestOperations
{
    /// <summary>Creates an open issue snapshot whose id, title, and slug are all <paramref name="id"/>.</summary>
    /// <param name="id">The item id.</param>
    /// <param name="blockers">The ids of the items that block it.</param>
    /// <param name="parent">The parent item id, or <see langword="null"/> for none.</param>
    public static RoadmapItemSnapshot Item(string id, IReadOnlyList<string> blockers, string? parent = null) =>
        new(id, id, id, "issue", "open", "theme", false, null, parent, null, null, null, null,
            blockers, [], [], [], [], null, false);

    /// <summary>Invokes the named non-public static method of <c>RepoConfigVerifier</c> on <paramref name="items"/> and returns the issues it collects.</summary>
    /// <param name="items">The item snapshots to check.</param>
    /// <param name="methodName">The name of the verifier method to invoke.</param>
    public static IReadOnlyList<RepoConfigIssue> Verify(RoadmapItemSnapshot[] items, string methodName)
    {
        var method = typeof(RepoConfigVerifier).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();
        List<RepoConfigIssue> issues = [];
        method.Invoke(null, [items, issues]);
        return issues;
    }
}
