namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests that the blocker and parent cycle checks report exactly the cycle members and use linear work and bounded stack space on large graphs.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapGraphTraversalTests
{
    /// <summary>In a 24-item graph where each item is blocked by its two predecessors, the blocker cycle check reports no issues and stays within 8 blocker reads per item.</summary>
    [Fact]
    public void Shared_acyclic_blockers_are_visited_in_linear_work()
    {
        // Arrange
        const int count = 24;
        var budget = new RoadmapGraphReadBudget(count * 8);
        var items = Enumerable.Range(0, count).Select(index => RoadmapGraphTestOperations.Item(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new CountingBlockerList(Enumerable.Range(Math.Max(0, index - 2), Math.Min(index, 2))
                .Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(), budget.RecordRead))).ToArray();

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyBlockedByCycles");

        // Assert
        issues.ShouldBeEmpty();
        budget.Reads.ShouldBeLessThanOrEqualTo(count * 8);
    }

    /// <summary>A chain of 20000 items each blocked by the previous one passes the blocker cycle check with no issues and within 8 reads per item.</summary>
    [Fact]
    public void Deep_acyclic_blockers_do_not_require_recursive_stack_space()
    {
        // Arrange
        const int count = 20000;
        var budget = new RoadmapGraphReadBudget(count * 8);
        var items = Enumerable.Range(0, count).Select(index => RoadmapGraphTestOperations.Item(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new CountingBlockerList(index == 0 ? [] : [(index - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)], budget.RecordRead))).ToArray();

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyBlockedByCycles");

        // Assert
        issues.ShouldBeEmpty();
        budget.Reads.ShouldBeLessThanOrEqualTo(count * 8);
    }

    /// <summary>With the cycle a, b, c plus d feeding into it, the blocker cycle check reports a, b, c, and d, but not the item that merely depends on the cycle or the item with a missing blocker.</summary>
    [Fact]
    public void All_cycle_members_are_reported_without_reporting_incoming_dependencies()
    {
        // Arrange
        RoadmapItemSnapshot[] items = [
            RoadmapGraphTestOperations.Item("root", ["a"]),
            RoadmapGraphTestOperations.Item("a", ["b", "d"]),
            RoadmapGraphTestOperations.Item("b", ["c"]),
            RoadmapGraphTestOperations.Item("c", ["a"]),
            RoadmapGraphTestOperations.Item("d", ["b"]),
            RoadmapGraphTestOperations.Item("missing", ["absent"])
        ];

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyBlockedByCycles");
        var paths = issues.Select(issue => issue.Path).Order(StringComparer.Ordinal).ToArray();

        // Assert
        paths.ShouldBe(["a", "b", "c", "d"]);
    }

    /// <summary>An item blocked by itself yields exactly one issue, "blockedBy cycle includes a."</summary>
    [Fact]
    public void Self_cycles_are_reported()
    {
        // Arrange
        RoadmapItemSnapshot[] items = [RoadmapGraphTestOperations.Item("a", ["a"])];

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyBlockedByCycles");

        // Assert
        issues.Count.ShouldBe(1);
        issues[0].Message.ShouldBe("blockedBy cycle includes a.");
    }

    /// <summary>A parent chain of 20000 items passes the parent cycle check with no issues.</summary>
    [Fact]
    public void Deep_parent_chains_do_not_require_recursive_stack_space()
    {
        // Arrange
        var items = Enumerable.Range(0, 20000).Select(index => RoadmapGraphTestOperations.Item(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture), [],
            index == 0 ? null : (index - 1).ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyParentCycles");

        // Assert
        issues.ShouldBeEmpty();
    }

    /// <summary>With parents a to b and b to a, and a child pointing at a, the parent cycle check reports a and b but not the child.</summary>
    [Fact]
    public void Parent_cycle_members_are_reported_without_incoming_children()
    {
        // Arrange
        RoadmapItemSnapshot[] items = [
            RoadmapGraphTestOperations.Item("child", [], "a"),
            RoadmapGraphTestOperations.Item("a", [], "b"),
            RoadmapGraphTestOperations.Item("b", [], "a")
        ];

        // Act
        var issues = RoadmapGraphTestOperations.Verify(items, "VerifyParentCycles");
        var paths = issues.Select(issue => issue.Path).Order(StringComparer.Ordinal).ToArray();

        // Assert
        paths.ShouldBe(["a", "b"]);
    }
}
