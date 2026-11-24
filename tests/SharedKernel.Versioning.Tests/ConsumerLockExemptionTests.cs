namespace PandaBear.Versioning.Tests;

/// <summary>Guards consumer verification of projects covered by the repository's lock exemption policy.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public sealed class ConsumerLockExemptionTests
{
    private const string LibraryProject = """
        <Project>
          <PropertyGroup>
            <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
          </PropertyGroup>
        </Project>
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_consumer_with_a_declared_lock_exemption_runs_the_command(bool namedSolution)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(LibraryProject);
        scope.DeclareExemption();
        var options = namedSolution ? new[] { "--solution", Path.Combine(scope.Root, "Consumer.slnx") } : [];
        var revision = await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct, options);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Error.ShouldContain(revision, StringComparison.Ordinal);
        result.Error.ShouldContain(ConsumerVerificationScope.Package, StringComparison.Ordinal);
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("passed");
        scope.Evidence.ContainsKey("reason").ShouldBeFalse();
    }

    [Theory]
    [InlineData("<Project />")]
    [InlineData(LibraryProject)]
    public async Task A_missing_lock_fails_without_a_declared_exemption(string project)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(project);
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        scope.AssertRejected(result, "packages.lock.json");
    }

    [Fact]
    public async Task A_declared_exemption_requires_the_project_to_disable_lock_files()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock("<Project />");
        scope.DeclareExemption();
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        scope.AssertRejected(result, "RestorePackagesWithLockFile");
    }

    [Fact]
    public async Task A_declared_exemption_conflicts_with_an_existing_lock_file()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(LibraryProject);
        scope.DeclareExemption();
        scope.WriteFile("src/Library/packages.lock.json", ConsumerVerificationScope.Lock("Direct", ConsumerVerificationScope.Version));
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        scope.AssertRejected(result, "must not keep a packages.lock.json");
    }

    [Theory]
    [InlineData("""<PackageReference Include="PandaBear.SharedKernel.Results" Version="0.9.0" />""")]
    [InlineData("""<PackageReference Update="PandaBear.SharedKernel.Results"><VersionOverride>0.9.0</VersionOverride></PackageReference>""")]
    public async Task An_exempt_project_cannot_declare_a_different_candidate_package_version(string packageReference)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(LibraryProject.Replace(
            "</Project>", "<ItemGroup>" + packageReference + "</ItemGroup></Project>", StringComparison.Ordinal));
        scope.DeclareExemption();
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        scope.AssertRejected(result, "PandaBear.SharedKernel.Results has version 0.9.0");
    }

    [Fact]
    public async Task A_source_reference_in_the_exempt_project_still_fails_before_running_the_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(LibraryProject.Replace(
            "</Project>",
            """<ItemGroup><ProjectReference Include="../SharedKernel.Results/SharedKernel.Results.csproj" /></ItemGroup></Project>""",
            StringComparison.Ordinal));
        scope.DeclareExemption();
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        scope.AssertRejected(result, ConsumerVerificationScope.LockFreeLibraryPath + " references SharedKernel source SharedKernel.Results");
    }

    [Fact]
    public async Task An_unselected_exemption_does_not_change_verification_of_a_named_solution_subset()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteLibraryWithoutLock(LibraryProject);
        scope.DeclareExemption();
        scope.WriteFile("LockBearing.slnx", """<Solution><Project Path="src/Consumer/Consumer.csproj" /></Solution>""");
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct, "--solution", Path.Combine(scope.Root, "LockBearing.slnx"));

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("passed");
    }
}
