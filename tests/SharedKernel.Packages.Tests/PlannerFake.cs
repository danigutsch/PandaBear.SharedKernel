namespace PandaBear.Packages.Tests;

/// <summary>Builds planners over fake sources.</summary>
internal static class PlannerFake
{
    /// <summary>The fixed time that planners decide at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a planner whose latest-version and release-date sources are the same fake.</summary>
    public static UpdatePlanner Planner(IAdvisorySource advisories, FakeRegistrySource registry) => new(advisories, registry, registry, new TestTimeProvider(Now));
}
