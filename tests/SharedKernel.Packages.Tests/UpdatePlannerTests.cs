using PandaBear.Results;
using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests that the planner joins the sources to the policy and fails when any source fails.</summary>
[Trait("Capability", "Packages")]
public sealed class UpdatePlannerTests
{
    private static readonly DateTimeOffset Now = PlannerFake.Now;
    private static readonly CancellationToken Cancel = TestContext.Current.CancellationToken;

    private static Result<IReadOnlyList<Advisory>> None => Result.Ok<IReadOnlyList<Advisory>>([]);

    [Fact]
    public async Task Find_updates_lists_only_packages_whose_latest_version_differs_ordered_by_name()
    {
        // Arrange
        var registry = new FakeRegistrySource(new()
        {
            ["bat"] = "0.26.1",
            ["fd"] = "10.0.0",
            ["ripgrep"] = "14.1.0"
        }, []);
        var planner = PlannerFake.Planner(new FakeAdvisorySource(None), registry);

        // Act
        var result = await planner.FindUpdates(PackageManagerKind.Cargo, [Installed("ripgrep", "14.1.0"), Installed("fd", "9.0.0"), Installed("bat", "0.24.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().Updates.Select(static u => u.ToString()).ShouldBe(["bat 0.24.0 -> 0.26.1", "fd 9.0.0 -> 10.0.0"]);
    }

    [Fact]
    public async Task A_failed_latest_lookup_fails_the_search_and_names_the_package()
    {
        // Arrange
        var planner = PlannerFake.Planner(new FakeAdvisorySource(None), new FakeRegistrySource([], []));

        // Act
        var result = await planner.FindUpdates(PackageManagerKind.Cargo, [Installed("bat", "1")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldStartWith("bat:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_the_registry_does_not_know_is_reported_and_the_others_are_still_searched()
    {
        // Arrange
        var registry = new FakeRegistrySource(new()
        {
            ["bat"] = "0.26.1",
            ["fd"] = "10.0.0"
        }, [], notFound: ["gitcrate"]);
        var planner = PlannerFake.Planner(new FakeAdvisorySource(None), registry);

        // Act
        var result = await planner.FindUpdates(PackageManagerKind.Cargo, [Installed("bat", "0.24.0"), Installed("gitcrate", "1.0.0"), Installed("fd", "9.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        var search = result.ShouldBeSuccess();
        search.Updates.Select(static u => u.Package.Value).ShouldBe(["bat", "fd"]);
        search.NotOnRegistry.Select(static n => n.Value).ShouldBe(["gitcrate"]);
    }

    [Fact]
    public async Task A_real_failure_after_a_not_found_package_still_fails_the_whole_search()
    {
        // Arrange
        var registry = new FakeRegistrySource(new()
        {
            ["bat"] = "0.26.1"
        }, [], notFound: ["aaa"]);
        var planner = PlannerFake.Planner(new FakeAdvisorySource(None), registry);

        // Act
        var result = await planner.FindUpdates(PackageManagerKind.Cargo, [Installed("aaa", "1"), Installed("bat", "0.24.0"), Installed("zzz", "1")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldStartWith("zzz:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_decides_with_the_policy_using_advisories_for_both_versions_and_the_release_date()
    {
        // Arrange
        var advisories = new FakeAdvisorySource(Result.Ok<IReadOnlyList<Advisory>>([AdvisoryFor("bat", "GHSA-1", "0.24.0")]));
        var registry = new FakeRegistrySource([], new()
        {
            ["bat"] = Now.AddDays(-1),
            ["fd"] = Now.AddDays(-30)
        });
        var planner = PlannerFake.Planner(advisories, registry);
        PackageUpdate[] updates = [new(Name("fd"), Version("9"), Version("10")), new(Name("bat"), Version("0.24.0"), Version("0.26.1"))];

        // Act
        var result = await planner.Plan(PackageManagerKind.Cargo, updates, Age(7), NetworkAccess.Allowed, Cancel);

        // Assert
        var decisions = result.ShouldBeSuccess();
        decisions.Select(static d => $"{d.Candidate.Package}:{d.Kind}").ShouldBe(["bat:Urgent", "fd:Allow"]);
        advisories.Asked!.Select(static p => p.ToString()).ShouldBe(["bat=0.24.0", "bat=0.26.1", "fd=9", "fd=10"]);
    }

    [Fact]
    public async Task A_failed_advisory_lookup_fails_the_whole_plan_and_no_decision_is_made()
    {
        // Arrange
        var advisories = new FakeAdvisorySource(Result.Unavailable<IReadOnlyList<Advisory>>("OSV is down"));
        var registry = new FakeRegistrySource([], new()
        {
            ["bat"] = Now.AddDays(-30)
        });

        // Act
        var result = await PlannerFake.Planner(advisories, registry).Plan(PackageManagerKind.Cargo, [new PackageUpdate(Name("bat"), Version("1"), Version("2"))], Age(7), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable");
    }

    [Fact]
    public async Task A_failed_release_date_lookup_fails_the_whole_plan_even_when_other_packages_succeed()
    {
        // Arrange
        var registry = new FakeRegistrySource([], new()
        {
            ["bat"] = Now.AddDays(-30),
            ["fd"] = Now.AddDays(-30)
        }, failingDateFor: "fd");
        PackageUpdate[] updates = [new(Name("bat"), Version("1"), Version("2")), new(Name("fd"), Version("1"), Version("2"))];

        // Act
        var result = await PlannerFake.Planner(new FakeAdvisorySource(None), registry).Plan(PackageManagerKind.Cargo, updates, Age(7), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldStartWith("fd:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_denied_network_fails_both_operations_before_any_lookup()
    {
        // Arrange
        var advisories = new FakeAdvisorySource(None);
        var planner = PlannerFake.Planner(advisories, new FakeRegistrySource([], []));

        // Act
        var find = await planner.FindUpdates(PackageManagerKind.Cargo, [Installed("bat", "1")], NetworkAccess.Denied, Cancel);
        var plan = await planner.Plan(PackageManagerKind.Cargo, [new PackageUpdate(Name("bat"), Version("1"), Version("2"))], Age(7), NetworkAccess.Denied, Cancel);

        // Assert
        find.ShouldBeFailure(ResultStatus.Forbidden, "forbidden");
        plan.ShouldBeFailure(ResultStatus.Forbidden, "forbidden");
        advisories.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Plan_with_no_updates_is_an_empty_success_without_asking_the_advisory_source()
    {
        // Arrange
        var advisories = new FakeAdvisorySource(None);

        // Act
        var result = await PlannerFake.Planner(advisories, new FakeRegistrySource([], [])).Plan(PackageManagerKind.Cargo, [], Age(7), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
        advisories.Calls.ShouldBe(0);
    }

    [Fact]
    public void A_package_update_rejects_an_unchanged_version_and_nulls_and_compares_by_value()
    {
        // Act
        Func<object?> unchanged = () => new PackageUpdate(Name("a"), Version("1"), Version("1"));
        Func<object?> nothing = () => new PackageUpdate(null!, Version("1"), Version("2"));

        // Assert
        unchanged.ShouldThrow<ArgumentException>();
        nothing.ShouldThrow<ArgumentNullException>();
        new PackageUpdate(Name("a"), Version("1"), Version("2")).ShouldBe(new PackageUpdate(Name("a"), Version("1"), Version("2")));
    }

    [Fact]
    public void The_planner_rejects_null_sources()
    {
        // Arrange
        var registry = new FakeRegistrySource([], []);

        // Act
        Func<object?> create = () => new UpdatePlanner(null!, registry, registry, TimeProvider.System);

        // Assert
        create.ShouldThrow<ArgumentNullException>();
    }
}
