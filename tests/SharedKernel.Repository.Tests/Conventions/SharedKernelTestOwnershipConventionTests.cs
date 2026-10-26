namespace PandaBear.Repository.Tests.Conventions;

public static class SharedKernelTestOwnershipConventionTests
{
    // Reviewed exceptions: a packable project with no test project in its reference closure, and the reason.
    private static readonly Dictionary<string, string> ReviewedExceptions = new(StringComparer.Ordinal)
    {
        ["SharedKernel.Functional"] = "Migration placeholder with no source files; the primitives live in SharedKernel.Results.",
    };

    [Fact]
    public static void Every_packable_source_project_is_reached_by_a_test_project_or_has_a_reviewed_exception()
    {
        // Arrange
        var unreached = SharedKernelTestOwnershipFiles.FindUnreachedPackableProjects();

        // Act
        var unowned = unreached
            .Where(name => !ReviewedExceptions.ContainsKey(name))
            .ToArray();

        // Assert
        unowned.ShouldBe([]);
    }

    [Fact]
    public static void Reviewed_exceptions_name_packable_projects_that_no_test_project_reaches()
    {
        // Arrange
        var unreached = SharedKernelTestOwnershipFiles.FindUnreachedPackableProjects();

        // Act
        var stale = ReviewedExceptions.Keys
            .Where(name => !unreached.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        stale.ShouldBe([]);
    }
}
