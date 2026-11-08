namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Guards the lock file policy: a library under <c>src</c> may drop its <c>packages.lock.json</c> only when it is listed in
/// <c>lockExemptProjects</c> of <c>.config/sharedkernel-repo.json</c> and opts out in its project file; every other project is locked.
/// Retention is minimal: every retained <c>src</c> library locks a package that no other lock file covers.
/// </summary>
public static class LockFilePolicyConventionTests
{
    [Fact]
    public static void Exempt_project_list_names_existing_unique_source_projects_in_order()
    {
        // Arrange
        var exempt = LockFilePolicyFiles.ReadExemptProjects();
        var projects = LockFilePolicyFiles.ReadProjects();

        // Act
        var unknown = exempt.Where(project => !projects.Contains(project)).ToArray();
        var outsideSource = exempt.Where(project => !project.StartsWith("src/", StringComparison.Ordinal)).ToArray();

        // Assert
        string.Join(", ", unknown).ShouldBe(string.Empty, "Every lockExemptProjects entry must be an existing project file.");
        string.Join(", ", outsideSource).ShouldBe(string.Empty, "Only packable libraries under src may be lock-exempt.");
        string.Join("\n", exempt).ShouldBe(string.Join("\n", exempt.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)), "Keep lockExemptProjects unique and sorted.");
    }

    [Fact]
    public static void Exempt_projects_have_no_lock_file_and_opt_out_of_locking()
    {
        // Arrange
        var exempt = LockFilePolicyFiles.ReadExemptProjects();

        // Act
        var withLock = exempt.Where(LockFilePolicyFiles.HasLockFile).ToArray();
        var notOptedOut = exempt.Where(project => LockFilePolicyFiles.ReadRestoreWithLockFile(project) != "false").ToArray();

        // Assert
        string.Join(", ", withLock).ShouldBe(string.Empty, "A lock-exempt project must not keep a packages.lock.json; NuGet reports NU1005 for it.");
        string.Join(", ", notOptedOut).ShouldBe(string.Empty, "A lock-exempt project must set RestorePackagesWithLockFile to false in its project file.");
    }

    [Fact]
    public static void Every_other_project_keeps_a_lock_file_and_does_not_opt_out()
    {
        // Arrange
        var exempt = LockFilePolicyFiles.ReadExemptProjects().ToHashSet(StringComparer.Ordinal);
        var locked = LockFilePolicyFiles.ReadProjects().Where(project => !exempt.Contains(project)).ToArray();

        // Act
        var withoutLock = locked.Where(project => !LockFilePolicyFiles.HasLockFile(project)).ToArray();
        var optedOut = locked.Where(project => LockFilePolicyFiles.ReadRestoreWithLockFile(project) == "false").ToArray();

        // Assert
        locked.ShouldNotBeEmpty();
        string.Join(", ", withoutLock).ShouldBe(string.Empty, "A project that is not in lockExemptProjects needs a packages.lock.json; add it to the list only when every package it resolves is locked by another project.");
        string.Join(", ", optedOut).ShouldBe(string.Empty, "A project that sets RestorePackagesWithLockFile to false must be listed in lockExemptProjects.");
    }

    [Fact]
    public static void Retained_source_libraries_each_lock_a_package_that_no_other_lock_covers()
    {
        // Arrange
        var exempt = LockFilePolicyFiles.ReadExemptProjects().ToHashSet(StringComparer.Ordinal);
        var locked = LockFilePolicyFiles.ReadProjects().Where(LockFilePolicyFiles.HasLockFile).ToArray();
        var pairs = locked.ToDictionary(project => project, LockFilePairs.ReadLocked, StringComparer.Ordinal);
        var retainedLibraries = locked.Where(project => project.StartsWith("src/", StringComparison.Ordinal) && !exempt.Contains(project)).ToArray();

        // Act
        var redundant = retainedLibraries
            .Where(project => pairs[project].IsSubsetOf(pairs.Where(other => other.Key != project).SelectMany(other => other.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        // Assert
        retainedLibraries.ShouldNotBeEmpty();
        string.Join(", ", redundant).ShouldBe(string.Empty, "A retained src library must lock a package that no other lock file covers; list it in lockExemptProjects (minimal retention, see docs/package-development.md).");
    }

    [Fact]
    public static void Restored_exempt_projects_resolve_only_packages_that_a_retained_lock_covers()
    {
        // Arrange
        var exempt = LockFilePolicyFiles.ReadExemptProjects();
        var covered = LockFilePolicyFiles.ReadProjects()
            .Where(LockFilePolicyFiles.HasLockFile)
            .SelectMany(LockFilePairs.ReadLocked)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var uncovered = exempt
            .SelectMany(project => (LockFilePairs.ReadRestored(project) ?? []).Where(pair => !covered.Contains(pair)).Select(pair => $"{project}: {pair}"))
            .ToArray();

        // Assert
        string.Join("\n", uncovered).ShouldBe(string.Empty, "A lock-exempt project may resolve only package versions that some committed lock file resolves, or the dependency snapshot would omit them. Projects that are not restored are skipped.");
    }
}
