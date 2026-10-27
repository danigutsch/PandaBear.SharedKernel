namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests that the blocker and parent cycle checks report exactly the cycle members and use linear work and bounded stack space on large graphs.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapGraphTraversalTests
{
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
