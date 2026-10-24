namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class LicenseExceptionCommandTests
{
    [Fact]
    public async Task Verify_license_exceptions_appends_the_approved_package_urls_when_every_check_passes()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        var outputPath = Path.Combine(repository.RootPath, "github-output");
        await File.WriteAllTextAsync(outputPath, "earlier=value\n", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", outputPath, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);
        var githubOutput = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
        result.StandardOutput.ShouldContain("License exceptions are current: pkg:nuget/StreamJsonRpc@2.25.29,pkg:nuget/Magick.NET-Q8-AnyCPU@15.0.0", StringComparison.Ordinal);
        githubOutput.ShouldBe("earlier=value\n" + LicenseExceptionTestRepository.ApprovedPackages + Environment.NewLine, "The package URLs keep the configured order and follow the existing outputs.");
    }

    [Theory]
    [InlineData("1.0.0-alpha.20261021.2", "2.25.29", "15.0.0", false, true, false, ".config/sharedkernel-candidate.json: packageVersion is \"1.0.0-alpha.20261021.2\", not the approved \"0.1.0-alpha.94.1\"; re-review the license exceptions.")]
    [InlineData("1.2.0-alpha.20261023.1", "2.25.29", "15.0.0", false, true, false, ".config/sharedkernel-candidate.json: packageVersion is \"1.2.0-alpha.20261023.1\", not the approved \"0.1.0-alpha.94.1\"; re-review the license exceptions.")]
    [InlineData("1.0.0", "2.25.29", "15.0.0", false, true, false, ".config/sharedkernel-candidate.json: packageVersion is \"1.0.0\", not the approved \"0.1.0-alpha.94.1\"; re-review the license exceptions.")]
    [InlineData("1.2.0-alpha.20261024.1", "2.25.29", "15.0.0", false, true, false, ".config/sharedkernel-candidate.json: packageVersion is \"1.2.0-alpha.20261024.1\", not the approved \"0.1.0-alpha.94.1\"; re-review the license exceptions.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.30", "15.0.0", false, true, false, "src/packages.lock.json: StreamJsonRpc resolves to 2.25.30 for net10.0, not the approved 2.25.29.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.29", "15.0.1", false, true, false, "src/packages.lock.json: Magick.NET-Q8-AnyCPU resolves to 15.0.1 for net10.0, not the approved 15.0.0.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.29", "15.0.0", true, true, false, ".config/sharedkernel-candidate.json: libraryDirty is true, not the approved false; re-review the license exceptions.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.29", "15.0.0", false, false, false, ".config/sharedkernel-candidate.json: libraryRevision is \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", not the approved \"d08008cd3e8901c304377edcc4683de7c3a85ecb\"; re-review the license exceptions.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.29", "15.0.0", false, true, true, "src/packages.lock.json: StreamJsonRpc resolves to 2.25.30 for net10.0/win-x64, not the approved 2.25.29.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "", "15.0.0", false, true, false, ".config/sharedkernel-repo.json: StreamJsonRpc 2.25.29 is approved, but no tracked packages.lock.json resolves it; remove its license exception.")]
    [InlineData(LicenseExceptionTestRepository.ApprovedVersion, "2.25.29", "", false, true, false, ".config/sharedkernel-repo.json: Magick.NET-Q8-AnyCPU 15.0.0 is approved, but no tracked packages.lock.json resolves it; remove its license exception.")]
    public async Task Verify_license_exceptions_requires_the_approved_values_and_every_locked_version(
        string candidateVersion,
        string dependencyVersion,
        string magickVersion,
        bool dirty,
        bool correctRevision,
        bool conflictingTarget,
        string expectedFinding)
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(candidateVersion, dependencyVersion, magickVersion, dirty, correctRevision, conflictingTarget));
        var outputPath = Path.Combine(repository.RootPath, "github-output");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", outputPath, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain("License exception verification failed:", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- " + expectedFinding, StringComparison.Ordinal);
        File.Exists(outputPath).ShouldBeFalse("A failed verification grants no license exception.");
    }

    [Fact]
    public async Task Verify_license_exceptions_matches_locked_package_ids_ignoring_case()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(PackageName: "streamjsonrpc", DependencyVersion: "2.25.30"));

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- src/packages.lock.json: streamjsonrpc resolves to 2.25.30 for net10.0, not the approved 2.25.29.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_license_exceptions_reads_only_tracked_lock_files()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        repository.WriteText("untracked/packages.lock.json", """{"version":2,"dependencies":{"net10.0":{"StreamJsonRpc":{"resolved":"9.9.9"}}}}""");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, "Git does not track the conflicting lock file.");
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Verify_license_exceptions_reads_tracked_lock_files_at_the_root_and_in_nested_directories()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        repository.WriteText("packages.lock.json", """{"version":2,"dependencies":{"net10.0":{"StreamJsonRpc":{"type":"Direct","resolved":"2.25.30"}}}}""");
        repository.WriteText("src/App/packages.lock.json", """{"version":2,"dependencies":{"net10.0":{"Magick.NET-Q8-AnyCPU":{"type":"Transitive","resolved":"15.0.1"}}}}""");
        await repository.Stage("packages.lock.json", TestContext.Current.CancellationToken);
        await repository.Stage("src/App/packages.lock.json", TestContext.Current.CancellationToken);
        var outputPath = Path.Combine(repository.RootPath, "github-output");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", outputPath, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, "src/packages.lock.json resolves the approved versions, so only the root and nested lock files can fail.");
        result.StandardError.ShouldContain("- packages.lock.json: StreamJsonRpc resolves to 2.25.30 for net10.0, not the approved 2.25.29.", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- src/App/packages.lock.json: Magick.NET-Q8-AnyCPU resolves to 15.0.1 for net10.0, not the approved 15.0.0.", StringComparison.Ordinal);
        File.Exists(outputPath).ShouldBeFalse("A failed verification grants no license exception.");
    }

    [Fact]
    public async Task Verify_license_exceptions_rejects_an_approval_file_that_repeats_a_property()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        repository.WriteText(
            ".config/sharedkernel-candidate.json",
            $$"""{"packageVersion":"1.0.0","packageVersion":"{{LicenseExceptionTestRepository.ApprovedVersion}}","libraryRevision":"{{LicenseExceptionTestRepository.ApprovedRevision}}","libraryDirty":false}""");
        var outputPath = Path.Combine(repository.RootPath, "github-output");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", outputPath, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, "The last packageVersion is the approved one, but the repeated property leaves the approved value ambiguous.");
        result.StandardError.ShouldContain("- .config/sharedkernel-candidate.json: Invalid JSON: Duplicate property 'packageVersion' encountered during deserialization.", StringComparison.Ordinal);
        File.Exists(outputPath).ShouldBeFalse("A failed verification grants no license exception.");
    }

    [Fact]
    public async Task Verify_license_exceptions_requires_a_tracked_lock_file()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(), stageLockFile: false);

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- .: Git tracks no packages.lock.json file.", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- .config/sharedkernel-repo.json: StreamJsonRpc 2.25.29 is approved, but no tracked packages.lock.json resolves it; remove its license exception.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_license_exceptions_resolves_a_relative_github_output_against_the_root()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        using var workingDirectory = new TemporaryDirectory();

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", "github-output", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            workingDirectory.Path);
        var githubOutput = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, "github-output"), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        githubOutput.ShouldBe(LicenseExceptionTestRepository.ApprovedPackages + Environment.NewLine);
        File.Exists(Path.Combine(workingDirectory.Path, "github-output")).ShouldBeFalse("A relative output path resolves against --root, not the working directory.");
    }

    [Fact]
    public async Task Verify_license_exceptions_requires_the_configuration_file()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        repository.Delete(".config/sharedkernel-repo.json");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json does not exist." + Environment.NewLine);
    }

    [Theory]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"licenseExceptions":null}""")]
    public async Task Verify_license_exceptions_requires_the_license_exceptions_section(string configuration)
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(), configuration);

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json has no licenseExceptions section." + Environment.NewLine);
    }

    [Theory]
    [InlineData("""{"version":2}""", ".config/sharedkernel-repo.json: $.version must be 1.")]
    [InlineData("""{}""", ".config/sharedkernel-repo.json is not valid at $ (line 1).")]
    [InlineData("""{"Version":1}""", ".config/sharedkernel-repo.json is not valid at $.Version (line 1).")]
    [InlineData("""{"version":1,"version":1}""", ".config/sharedkernel-repo.json is not valid at $.version (line 1).")]
    [InlineData("{\"version\":1 // comment\n}", ".config/sharedkernel-repo.json is not valid at $ (line 1).")]
    [InlineData("""{"version":1,}""", ".config/sharedkernel-repo.json is not valid at $ (line 1).")]
    [InlineData("""{"version":"1"}""", ".config/sharedkernel-repo.json is not valid at $.version (line 1).")]
    [InlineData("""{"version":1,"licenseException":{}}""", ".config/sharedkernel-repo.json is not valid at $.licenseException (line 1).")]
    [InlineData("""{"version":1,"licenseExceptions":{"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json is not valid at $.licenseExceptions (line 1).")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"},"extra":1},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json is not valid at $.licenseExceptions.approval.extra (line 1).")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"../pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"/etc/pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":".config\\pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"C:/pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"./pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"config//pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.values must name at least one property.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"packageVersion":1}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.values['packageVersion'] must be a string or a boolean.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"":"b"}},"packages":{"A":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.approval.values[''] must name a property.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"}},"packages":{}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.packages must name at least one package.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0","a":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.packages['a'] repeats a package ID that differs only in case.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"}},"packages":{"A,B":"1.0.0"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.packages['A,B'] must be a NuGet package ID.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"}},"packages":{"A":"1.0.0\npackages=other"}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.packages['A'] must be a NuGet version.")]
    [InlineData("""{"version":1,"licenseExceptions":{"approval":{"file":"pin.json","values":{"a":"b"}},"packages":{"A":null}}}""", ".config/sharedkernel-repo.json: $.licenseExceptions.packages['A'] must be a NuGet version.")]
    public async Task Verify_license_exceptions_names_the_file_and_json_path_of_an_invalid_configuration(string configuration, string expectedError)
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(), configuration);
        var outputPath = Path.Combine(repository.RootPath, "github-output");

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", outputPath, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldStartWith("sharedkernel-repo: " + expectedError, StringComparison.Ordinal);
        File.Exists(outputPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_license_exceptions_rejects_a_configuration_larger_than_one_mebibyte()
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario(), LicenseExceptionTestRepository.Configuration + new string(' ', 1024 * 1024));

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json exceeds the 1 MiB configuration size limit." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_license_exceptions_does_not_follow_a_symbolic_link_to_the_approval_file()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        using var outside = new TemporaryDirectory();
        var outsidePin = Path.Combine(outside.Path, "pin.json");
        File.Copy(Path.Combine(repository.RootPath, ".config", "sharedkernel-candidate.json"), outsidePin);
        repository.Delete(".config/sharedkernel-candidate.json");
        File.CreateSymbolicLink(Path.Combine(repository.RootPath, ".config", "sharedkernel-candidate.json"), outsidePin);

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- .config/sharedkernel-candidate.json: Symbolic links are not followed in repository paths.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_license_exceptions_does_not_follow_a_symbolic_link_to_the_configuration()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        using var outside = new TemporaryDirectory();
        var outsideConfiguration = Path.Combine(outside.Path, "sharedkernel-repo.json");
        File.Copy(Path.Combine(repository.RootPath, ".config", "sharedkernel-repo.json"), outsideConfiguration);
        repository.Delete(".config/sharedkernel-repo.json");
        File.CreateSymbolicLink(Path.Combine(repository.RootPath, ".config", "sharedkernel-repo.json"), outsideConfiguration);

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json: Symbolic links are not followed in repository paths." + Environment.NewLine);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Verify_license_exceptions_rejects_a_blank_github_output_path(string githubOutput)
    {
        // Arrange
        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());

        // Act
        var result = await repository.RunCommand(
            ["dependency", "verify-license-exceptions", "--github-output", githubOutput, "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("--github-output requires a file path.", StringComparison.Ordinal);
    }
}
