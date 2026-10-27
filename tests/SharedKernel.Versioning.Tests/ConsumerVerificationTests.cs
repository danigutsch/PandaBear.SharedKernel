using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

/// <summary>Guards <c>verify-consumer</c>.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public sealed class ConsumerVerificationTests
{
    /// <summary>
    /// Verifies that the command runs in a clean clone of the committed revision with the candidate packages and isolated NuGet caches, records passed evidence, and leaves the manifest and scratch space clean.
    /// </summary>
    [Fact]
    public async Task The_command_runs_on_a_clean_clone_of_the_committed_revision_with_the_candidate_packages_and_isolated_caches()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        var revision = await scope.Commit(ct);
        var manifest = await File.ReadAllTextAsync(scope.Manifest, ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldBe("Consumer compatibility: passed. Evidence: " + scope.EvidencePath + Environment.NewLine);
        result.Error.ShouldContain(ConsumerVerificationScope.Package, StringComparison.Ordinal);
        result.Error.ShouldContain(revision, StringComparison.Ordinal);
        var scratch = ConsumerVerificationScope.ScratchDirectory(result.Error);
        result.Error.ShouldContain("/consumer", StringComparison.Ordinal);
        result.Error.ShouldMatch("packages=.*" + Path.GetFileName(scratch) + ".packages");
        result.Error.ShouldMatch("http=.*" + Path.GetFileName(scratch) + ".http-cache");
        Directory.Exists(scratch).ShouldBeFalse();
        Directory.Exists(Path.Combine(scope.Root, ".local")).ShouldBeFalse();
        var evidence = scope.Evidence;
        evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("passed");
        evidence["consumerRevision"]!.GetValue<string>().ShouldBe(revision);
        evidence["consumerDirty"]!.GetValue<bool>().ShouldBeFalse();
        evidence["packageVersion"]!.GetValue<string>().ShouldBe(ConsumerVerificationScope.Version);
        evidence.ContainsKey("reason").ShouldBeFalse();
        var unchanged = await File.ReadAllTextAsync(scope.Manifest, ct);
        unchanged.ShouldBe(manifest);
    }

    /// <summary>
    /// Verifies that uncommitted consumer changes exit 2 with pending dirty evidence and the command is not run.
    /// </summary>
    [Fact]
    public async Task Uncommitted_consumer_changes_stay_pending_without_running_the_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        await scope.Commit(ct);
        scope.WriteFile("src/Consumer/Consumer.csproj", "<Project>changed</Project>");

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Error: Compatibility requires committed library and consumer revisions", StringComparison.Ordinal);
        result.Error.ShouldNotContain("packages=", StringComparison.Ordinal);
        var evidence = scope.Evidence;
        evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("pending");
        evidence["consumerDirty"]!.GetValue<bool>().ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that a dirty library revision in the candidate exits 2 with pending evidence and the command is not run.
    /// </summary>
    [Fact]
    public async Task Uncommitted_library_evidence_stays_pending_without_running_the_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.Candidate["libraryDirty"] = true;
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldNotContain("packages=", StringComparison.Ordinal);
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("pending");
        scope.Evidence["consumerDirty"]!.GetValue<bool>().ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that a failing command exits 2, records failed evidence with the reason, and deletes the scratch directory.
    /// </summary>
    [Fact]
    public async Task A_failing_command_records_failed_evidence_and_deletes_the_scratch_directory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe("; exit 3"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Error: Command failed (3): git -c alias.probe=", StringComparison.Ordinal);
        Directory.Exists(ConsumerVerificationScope.ScratchDirectory(result.Error)).ShouldBeFalse();
        var evidence = scope.Evidence;
        evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("failed");
        evidence["reason"]!.GetValue<string>().ShouldStartWith("Command failed (3): ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a consumer whose files do not resolve every SharedKernel package to the candidate (stale versions, project references, bad NuGet.Config, committed feed packages, or an outside solution project) fails with the expected message without running the command.
    /// </summary>
    [Theory]
    [InlineData("Directory.Packages.props", """<Project><PackageVersion Include="pandabear.sharedkernel.results" Version="0.9.0" /></Project>""", "Directory.Packages.props: pandabear.sharedkernel.results has version 0.9.0, not the candidate version 1.0.0-alpha.1.")]
    [InlineData("src/Consumer/packages.lock.json", "stale-lock", "src/Consumer/packages.lock.json: PandaBear.SharedKernel.Results resolves to 0.9.0, not the candidate version 1.0.0-alpha.1.")]
    [InlineData("src/Consumer/packages.lock.json", "project-lock", "src/Consumer/packages.lock.json references SharedKernel source PandaBear.SharedKernel.Results instead of the candidate's package.")]
    [InlineData("src/Consumer/packages.lock.json", "{}", "src/Consumer/packages.lock.json has no dependencies.")]
    [InlineData("src/Consumer/Consumer.csproj", """<Project><ItemGroup><ProjectReference Include="..\..\lib\SharedKernel.Results\SharedKernel.Results.csproj" /></ItemGroup></Project>""", "src/Consumer/Consumer.csproj references SharedKernel source SharedKernel.Results instead of the candidate's package.")]
    [InlineData(".config/dotnet-tools.json", """{ "version": 1, "isRoot": true, "tools": { "PANDABEAR.SHAREDKERNEL.CI.TOOL": { "version": "0.9.0", "commands": ["sharedkernel-ci"] } } }""", ".config/dotnet-tools.json: PANDABEAR.SHAREDKERNEL.CI.TOOL has version 0.9.0, not the candidate version 1.0.0-alpha.1.")]
    [InlineData("Consumer.slnx", """<Solution><Project Path="../Outside/Outside.csproj" /></Solution>""", "Consumer.slnx lists a project outside the repository: ../Outside/Outside.csproj.")]
    [InlineData("Second.slnx", "<Solution />", "The repository root has 2 .slnx files; name one with --solution.")]
    [InlineData("NuGet.Config", "no-source-clear", "NuGet.Config must clear packageSources and fallbackPackageFolders")]
    [InlineData("NuGet.Config", "no-fallback-clear", "NuGet.Config must clear packageSources and fallbackPackageFolders")]
    [InlineData("NuGet.Config", "no-mapping", "NuGet.Config must map PandaBear.SharedKernel.* to exactly one package source.")]
    [InlineData("NuGet.Config", "second-mapping", "NuGet.Config must map PandaBear.SharedKernel.* to exactly one package source.")]
    [InlineData("NuGet.Config", "rooted-feed", "NuGet.Config must add the source sharedkernel of PandaBear.SharedKernel.* once, as a folder inside the repository")]
    [InlineData("NuGet.Config", "parent-feed", "NuGet.Config must add the source sharedkernel of PandaBear.SharedKernel.* once, as a folder inside the repository")]
    [InlineData("feed/Old.nupkg", "committed package", "The committed package folder feed already holds packages, which restore could use instead of the candidate's.")]
    public async Task A_consumer_that_does_not_resolve_every_package_to_the_candidate_fails_without_running_the_command(string path, string content, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        var replacement = content switch
        {
            "stale-lock" => ConsumerVerificationScope.Lock("Direct", "0.9.0"),
            "project-lock" => ConsumerVerificationScope.Lock("Project", ConsumerVerificationScope.Version),
            "no-source-clear" => ConsumerVerificationScope.NuGetConfig.Replace("<packageSources>\n    <clear />", "<packageSources>", StringComparison.Ordinal),
            "no-fallback-clear" => ConsumerVerificationScope.NuGetConfig.Replace("<fallbackPackageFolders><clear /></fallbackPackageFolders>", string.Empty, StringComparison.Ordinal),
            "no-mapping" => ConsumerVerificationScope.NuGetConfig.Replace("<package pattern=\"PandaBear.SharedKernel.*\" />", string.Empty, StringComparison.Ordinal),
            "second-mapping" => ConsumerVerificationScope.NuGetConfig.Replace("<package pattern=\"*\" />", "<package pattern=\"*\" /><package pattern=\"pandabear.sharedkernel.results\" />", StringComparison.Ordinal),
            "rooted-feed" => ConsumerVerificationScope.NuGetConfig.Replace(".local/sharedkernel-feed", Path.GetTempPath(), StringComparison.Ordinal),
            "parent-feed" => ConsumerVerificationScope.NuGetConfig.Replace(".local/sharedkernel-feed", "../feed", StringComparison.Ordinal),
            _ => content,
        };
        if (path == "feed/Old.nupkg")
        {
            scope.WriteFile("NuGet.Config", ConsumerVerificationScope.NuGetConfig.Replace(".local/sharedkernel-feed", "feed", StringComparison.Ordinal));
        }

        scope.WriteFile(path, replacement);
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Error: " + expected, StringComparison.Ordinal);
        result.Error.ShouldNotContain("packages=", StringComparison.Ordinal);
        var evidence = scope.Evidence;
        evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("failed");
        evidence["reason"]!.GetValue<string>().ShouldContain(expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that --solution selects the solution to check when the root has more than one .slnx file.
    /// </summary>
    [Fact]
    public async Task A_named_solution_is_checked_instead_of_requiring_one_at_the_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile("Second.slnx", "<Solution />");
        await scope.Commit(ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct, "--solution", Path.Combine(scope.Root, "Consumer.slnx"));

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
    }

    /// <summary>
    /// Verifies that an invalid candidate manifest or package exits 2 and records failed evidence with the consumer revision.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("tampered")]
    [InlineData("no-version")]
    [InlineData("no-library-state")]
    public async Task An_invalid_candidate_records_failed_evidence_with_the_consumer_revision(string problem)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        if (problem == "no-version")
        {
            scope.Candidate.Remove("packageVersion");
        }

        if (problem == "no-library-state")
        {
            scope.Candidate["libraryDirty"] = null;
        }

        var revision = await scope.Commit(ct);
        if (problem == "tampered")
        {
            await File.WriteAllTextAsync(Path.Combine(scope.CandidateDirectory, ConsumerVerificationScope.Package), "changed", ct);
        }
        else if (problem is not ("no-version" or "no-library-state"))
        {
            await File.WriteAllTextAsync(scope.Manifest, problem, ct);
        }

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldStartWith("Error: ", StringComparison.Ordinal);
        result.Error.ShouldNotContain("packages=", StringComparison.Ordinal);
        var evidence = scope.Evidence;
        evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("failed");
        evidence["consumerRevision"]!.GetValue<string>().ShouldBe(revision);
        evidence["reason"]!.GetValue<string>().ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies that --output naming the candidate manifest is rejected, the manifest stays unchanged, and no evidence is written.
    /// </summary>
    [Fact]
    public async Task The_evidence_never_replaces_the_candidate_manifest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        await scope.Commit(ct);
        var manifest = await File.ReadAllTextAsync(scope.Manifest, ct);

        // Act
        var result = await scope.Verify(ConsumerVerificationScope.Probe(), ct, "--output", scope.Manifest);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe("Error: --output names the candidate manifest, which keeps the evidence that the library recorded." + Environment.NewLine);
        var unchanged = await File.ReadAllTextAsync(scope.Manifest, ct);
        unchanged.ShouldBe(manifest);
        File.Exists(scope.EvidencePath).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that cancelling stops the running command, deletes the scratch directory, and writes no evidence.
    /// </summary>
    [Fact]
    public async Task Cancellation_stops_the_command_deletes_the_scratch_directory_and_writes_no_evidence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        await scope.Commit(ct);
        var marker = Path.Combine(scope.CandidateDirectory, "started.txt");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var run = scope.Verify(["git", "-c", "alias.wait=!pwd > '" + marker.Replace('\\', '/') + "'; sleep 60", "wait"], cancellation.Token);
        for (var attempt = 0; attempt < 600 && (!File.Exists(marker) || new FileInfo(marker).Length == 0) && !run.IsCompleted; attempt++)
        {
            await Task.Delay(50, ct);
        }

        var scratch = ConsumerVerificationScope.ScratchDirectory(await File.ReadAllTextAsync(marker, ct));
        Directory.Exists(scratch).ShouldBeTrue();

        // Act
        await cancellation.CancelAsync();
        var result = await run;

        // Assert
        result.ExitCode.ShouldBe(1);
        Directory.Exists(scratch).ShouldBeFalse();
        File.Exists(scope.EvidencePath).ShouldBeFalse();
    }
}
