namespace PandaBear.Versioning.Tests;

/// <summary>Tests which projects the incremental local pack rebuilds, in which order, and the cache eviction guard.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class SharedKernelDevPackPlannerTests
{
    private const string Version = "0.1.0-dev.local";

    [Fact]
    public static void Unknown_projects_are_rejected()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results");

        // Act
        Action act = () => SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Nope"], repository.Feed, Version);

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("SharedKernel.Nope", StringComparison.Ordinal);
    }

    [Fact]
    public static void A_named_project_is_rebuilt_after_its_missing_dependency()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddProject("SharedKernel.Domain", "PandaBear.SharedKernel.Domain", "SharedKernel.Results");

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Domain"], repository.Feed, Version);

        // Assert
        steps.Select(step => step.Name).ShouldBe(["SharedKernel.Results", "SharedKernel.Domain"]);
        steps.All(step => step.Rebuild).ShouldBeTrue();
    }

    [Fact]
    public static void A_dependency_already_in_the_feed_is_kept()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddProject("SharedKernel.Domain", "PandaBear.SharedKernel.Domain", "SharedKernel.Results");
        repository.AddFeedPackage("PandaBear.SharedKernel.Results", Version);

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Domain"], repository.Feed, Version);

        // Assert
        steps.Where(step => step.Name == "SharedKernel.Results").ToList().ShouldHaveSingleItem().Rebuild.ShouldBeFalse();
        steps.Where(step => step.Name == "SharedKernel.Domain").ToList().ShouldHaveSingleItem().Rebuild.ShouldBeTrue();
    }

    [Fact]
    public static void A_named_project_is_rebuilt_even_when_the_feed_holds_it()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddFeedPackage("PandaBear.SharedKernel.Results", Version);

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["sharedkernel.results"], repository.Feed, Version);

        // Assert
        steps.ShouldHaveSingleItem().Rebuild.ShouldBeTrue();
    }

    [Fact]
    public static void No_request_rebuilds_every_project_dependencies_first()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.A", null, "SharedKernel.B");
        repository.AddProject("SharedKernel.B");
        repository.AddFeedPackage("SharedKernel.B", Version);

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, [], repository.Feed, Version);

        // Assert
        steps.Select(step => step.Name).ShouldBe(["SharedKernel.B", "SharedKernel.A"]);
        steps.All(step => step.Rebuild).ShouldBeTrue();
    }

    [Fact]
    public static void The_package_id_comes_from_the_project_file_with_the_project_name_as_fallback()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddProject("SharedKernel.Plain");

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, [], repository.Feed, Version);

        // Assert
        steps.Select(step => step.PackageId).ShouldBe(["SharedKernel.Plain", "PandaBear.SharedKernel.Results"]);
    }

    [Fact]
    public static void References_to_unknown_projects_are_ignored()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results");
        repository.AddExternalReference("SharedKernel.Results");

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Results"], repository.Feed, Version);

        // Assert
        steps.ShouldHaveSingleItem().Name.ShouldBe("SharedKernel.Results");
    }

    [Fact]
    public static void Reference_cycles_terminate()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.A", null, "SharedKernel.B");
        repository.AddProject("SharedKernel.B", null, "SharedKernel.A");

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.A"], repository.Feed, Version);

        // Assert
        steps.Count.ShouldBe(2);
    }

    [Fact]
    public static void Planning_requires_arguments()
    {
        // Arrange
        Action blankRoot = () => SharedKernelDevPackPlanner.Plan(" ", [], "feed", Version);
        Action noRequest = () => SharedKernelDevPackPlanner.Plan("root", null!, "feed", Version);

        // Act & Assert
        blankRoot.ShouldThrow<ArgumentException>();
        noRequest.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static void Eviction_removes_only_the_cached_version()
    {
        // Arrange
        using var repository = new DevPackRepository();
        var cache = repository.Root;
        var cachedVersion = Directory.CreateDirectory(Path.Combine(cache, "pandabear.sharedkernel.results", "0.1.0-dev.local"));
        var otherVersion = Directory.CreateDirectory(Path.Combine(cache, "pandabear.sharedkernel.results", "0.1.0-alpha.1"));

        // Act
        var removed = SharedKernelDevPackPlanner.EvictFromGlobalPackages("PandaBear.SharedKernel.Results", Version, cache);
        var removedAgain = SharedKernelDevPackPlanner.EvictFromGlobalPackages("PandaBear.SharedKernel.Results", Version, cache);

        // Assert
        removed.ShouldBeTrue();
        removedAgain.ShouldBeFalse();
        cachedVersion.Refresh();
        otherVersion.Refresh();
        cachedVersion.Exists.ShouldBeFalse();
        otherVersion.Exists.ShouldBeTrue();
    }

    [Theory]
    [InlineData("..", Version)]
    [InlineData("a/b", Version)]
    [InlineData("a\\b", Version)]
    [InlineData("PandaBear.SharedKernel.Results", "../x")]
    [InlineData("", Version)]
    public static void Eviction_rejects_anything_but_a_single_path_segment(string packageId, string version)
    {
        // Arrange
        Action act = () => SharedKernelDevPackPlanner.EvictFromGlobalPackages(packageId, version, Path.GetTempPath());

        // Act & Assert
        act.ShouldThrow<ArgumentException>();
    }

    /// <summary>An explicit directory beats the environment and the dotnet query.</summary>
    [Fact]
    public static async Task An_explicit_directory_wins()
    {
        // Arrange
        var consumer = Path.GetFullPath("consumer");
        var explicitDirectory = Path.GetFullPath("explicit");

        // Act
        var folder = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            consumer, explicitDirectory, Path.GetFullPath("env"), GlobalPackagesFakes.Reporting(Path.GetFullPath("config")), TestContext.Current.CancellationToken);

        // Assert
        folder.ShouldBe(explicitDirectory);
    }

    /// <summary>A non-empty environment value beats the dotnet query.</summary>
    [Fact]
    public static async Task A_non_empty_environment_value_wins_over_the_dotnet_query()
    {
        // Arrange
        var environment = Path.GetFullPath("env");

        // Act
        var folder = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            Path.GetFullPath("consumer"), null, environment, GlobalPackagesFakes.Reporting(Path.GetFullPath("config")), TestContext.Current.CancellationToken);

        // Assert
        folder.ShouldBe(environment);
    }

    /// <summary>An empty or blank environment value counts as unset, so the folder from NuGet configuration is used.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public static async Task An_unset_environment_value_uses_the_folder_nuget_reports(string? environment)
    {
        // Arrange
        var configured = Path.GetFullPath("config");

        // Act
        var folder = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            Path.GetFullPath("consumer"), null, environment, GlobalPackagesFakes.Reporting(configured), TestContext.Current.CancellationToken);

        // Assert
        folder.ShouldBe(configured);
    }

    /// <summary>Without any configuration the default folder under the user profile is used.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task The_default_folder_is_the_last_resort(bool queryReturnsNothing)
    {
        // Arrange
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        Func<CancellationToken, Task<string?>>? query = queryReturnsNothing ? static _ => Task.FromResult<string?>(null) : null;

        // Act
        var folder = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            Path.GetFullPath("consumer"), null, "", query, TestContext.Current.CancellationToken);

        // Assert
        folder.ShouldBe(expected);
    }

    /// <summary>A relative result is made absolute against the consumer directory.</summary>
    [Fact]
    public static async Task A_relative_folder_is_made_absolute_against_the_consumer()
    {
        // Arrange
        var consumer = Path.GetFullPath("consumer");

        // Act
        var folder = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            consumer, null, null, GlobalPackagesFakes.Reporting(Path.Combine("..", "shared", "packages")), TestContext.Current.CancellationToken);

        // Assert
        folder.ShouldBe(Path.GetFullPath(Path.Combine(consumer, "..", "shared", "packages")));
    }

    /// <summary>The listing parser reads the <c>global-packages:</c> line and ignores everything else.</summary>
    [Theory]
    [InlineData("global-packages: /home/me/.nuget/packages/\n", "/home/me/.nuget/packages/")]
    [InlineData("info: x\nglobal-packages: /a b/c\r\nhttp-cache: /d\n", "/a b/c")]
    [InlineData("http-cache: /d\n", null)]
    [InlineData("", null)]
    [InlineData("global-packages:   \n", null)]
    public static void The_listing_parser_finds_the_global_packages_line(string output, string? expected)
    {
        // Arrange & Act
        var folder = SharedKernelDevPackPlanner.ParseGlobalPackagesListing(output);

        // Assert
        folder.ShouldBe(expected);
    }

    private static readonly DateTime Packed = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Query.cs", 1, true)]
    [InlineData("Query.cs", -1, false)]
    [InlineData("Extra.props", 1, true)]
    [InlineData("Extra.targets", 1, true)]
    [InlineData("readme.md", 1, false)]
    [InlineData("bin/Debug/Generated.cs", 1, false)]
    [InlineData("obj/Generated.cs", 1, false)]
    public static void A_kept_dependency_is_stale_only_when_a_source_file_is_newer_than_its_package(string file, int hoursAfterPackage, bool expected)
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddProject("SharedKernel.Domain", "PandaBear.SharedKernel.Domain", "SharedKernel.Results");
        repository.AddFeedPackage("PandaBear.SharedKernel.Results", Version);
        repository.SetProjectFileTimes("SharedKernel.Results", Packed.AddDays(-1));
        repository.AddProjectFile("SharedKernel.Results", file, Packed.AddHours(hoursAfterPackage));
        repository.SetFeedPackageTime("PandaBear.SharedKernel.Results", Version, Packed);

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Domain"], repository.Feed, Version);

        // Assert
        var results = steps.Where(step => step.Name == "SharedKernel.Results").ToList().ShouldHaveSingleItem();
        results.Rebuild.ShouldBeFalse();
        results.Stale.ShouldBe(expected);
    }

    [Fact]
    public static void Rebuilt_steps_are_never_stale()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        repository.AddProject("SharedKernel.Domain", "PandaBear.SharedKernel.Domain", "SharedKernel.Results");
        repository.AddFeedPackage("PandaBear.SharedKernel.Domain", Version);
        repository.SetFeedPackageTime("PandaBear.SharedKernel.Domain", Version, Packed.AddDays(-1));
        repository.AddProjectFile("SharedKernel.Domain", "Newer.cs", Packed);

        // Act
        var steps = SharedKernelDevPackPlanner.Plan(repository.Root, ["SharedKernel.Domain"], repository.Feed, Version);

        // Assert
        steps.All(step => step.Rebuild).ShouldBeTrue();
        steps.Any(step => step.Stale).ShouldBeFalse();
    }
}
