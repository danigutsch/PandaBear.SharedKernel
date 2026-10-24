namespace PandaBear.Repository.Tests;

public sealed class CandidateStagingCheckTests
{
    [Theory]
    [InlineData("oversized", "is larger than 1 MiB")]
    [InlineData("malformed", "is not a valid candidate manifest")]
    [InlineData("comment", "is not a valid candidate manifest")]
    [InlineData("trailing-comma", "is not a valid candidate manifest")]
    [InlineData("duplicate-property", "is not a valid candidate manifest")]
    [InlineData("null", "is not a valid candidate manifest")]
    [InlineData("missing-revision", "libraryRevision")]
    [InlineData("uppercase-revision", "libraryRevision must be 40 lowercase hexadecimal characters")]
    [InlineData("short-revision", "libraryRevision must be 40 lowercase hexadecimal characters")]
    [InlineData("dirty", "libraryDirty must be false")]
    [InlineData("dirty-string", "is not a valid candidate manifest")]
    [InlineData("version-separator", "packageVersion may contain only letters, digits, '.', and '-'")]
    [InlineData("version-empty", "packageVersion may contain only letters, digits, '.', and '-'")]
    [InlineData("no-packages", "packageSha256 must name at least one package")]
    [InlineData("uppercase-hash", "must be 64 lowercase hexadecimal characters")]
    [InlineData("null-hash", "must be 64 lowercase hexadecimal characters")]
    [InlineData("name-prefix", "does not match PandaBear.SharedKernel.<id>.1.2.3-alpha.4.1.nupkg")]
    [InlineData("name-version", "does not match PandaBear.SharedKernel.<id>.1.2.3-alpha.4.1.nupkg")]
    [InlineData("name-traversal", "does not match PandaBear.SharedKernel.<id>.1.2.3-alpha.4.1.nupkg")]
    [InlineData("name-control", "PandaBear.SharedKernel.Results\\u001B[31m.1.2.3-alpha.4.1.nupkg does not match")]
    [InlineData("name-case-duplicate", "differ only in case")]
    public async Task Invalid_pins_fail_before_git_runs_or_a_file_changes(string scenario, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        CandidatePinCases.Write(scope, scenario);
        var git = new CandidateGit();
        using var server = FakeReleaseServer.For(scope);

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(git, server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        result.Error.ShouldNotContain("\u001B", StringComparison.Ordinal);
        git.Calls.ShouldBeEmpty();
        server.Requests.ShouldBeEmpty();
        Directory.Exists(Path.GetDirectoryName(scope.Output)).ShouldBeFalse("A pin is validated before any write.");
    }

    [Theory]
    [InlineData("""{ "sdk": { "version": "10.0.401", "rollForward": "disable" } }""", "sdk.version 10.0.401 and sdk.rollForward disable")]
    [InlineData("""{ "sdk": { "version": "10.0.402", "rollForward": "latestPatch" } }""", "sdk.version 10.0.402 and sdk.rollForward latestPatch")]
    [InlineData("""{ "sdk": { "version": "10.0.402" } }""", "sdk.version 10.0.402 and sdk.rollForward (not set)")]
    public async Task Sdk_lockstep_compares_both_global_json_files_before_git_runs(string libraryGlobalJson, string expectedLibraryValues)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        await File.WriteAllTextAsync(Path.Combine(scope.LibraryRoot, "global.json"), libraryGlobalJson, TestContext.Current.CancellationToken);
        var git = new CandidateGit();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(Path.Combine(scope.Root, "global.json") + " has sdk.version 10.0.402 and sdk.rollForward disable", StringComparison.Ordinal);
        result.Error.ShouldContain(Path.Combine(scope.LibraryRoot, "global.json") + " has " + expectedLibraryValues, StringComparison.Ordinal);
        git.Calls.ShouldBeEmpty();
        Directory.Exists(scope.Output).ShouldBeFalse();
    }

    [Fact]
    public async Task Sdk_lockstep_requires_the_consumer_global_json()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        File.Delete(Path.Combine(scope.Root, "global.json"));
        var git = new CandidateGit();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(Path.Combine(scope.Root, "global.json"), StringComparison.Ordinal);
        git.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("true\nsub/\n", 1, "is not the top level of a Git work tree")]
    [InlineData("false\n", 1, "is not the top level of a Git work tree")]
    [InlineData(null, 2, "HEAD is " + "fedcba9876543210fedcba9876543210fedcba98" + ", but the pin names libraryRevision " + CandidateStagingScope.Revision)]
    public async Task Library_checkout_must_be_the_top_level_at_the_pinned_revision(string? location, int expectedGitCalls, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var git = new CandidateGit { Head = "fedcba9876543210fedcba9876543210fedcba98\n" };
        if (location is not null)
        {
            git.Location = location;
        }

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        git.Calls.Count.ShouldBe(expectedGitCalls, "Each check runs only after the previous one passed.");
        Directory.Exists(scope.Output).ShouldBeFalse();
    }

    [Fact]
    public async Task Library_checkout_must_be_clean()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var git = new CandidateGit { Status = " M src/Changed.cs\n?? untracked\u001B[2J.txt\n" };

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("has uncommitted changes", StringComparison.Ordinal);
        result.Error.ShouldContain(" M src/Changed.cs", StringComparison.Ordinal);
        result.Error.ShouldContain("?? untracked\\u001B[2J.txt", StringComparison.Ordinal);
        Directory.Exists(scope.Output).ShouldBeFalse();
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("access")]
    [InlineData("io")]
    [InlineData("http")]
    public async Task Expected_failures_exit_with_code_2_and_an_escaped_message(string failure)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        const string message = "git failed\u001B[31m\u202E";
        var git = new CandidateGit
        {
            Failure = failure switch
            {
                "timeout" => new TimeoutException(message),
                "access" => new UnauthorizedAccessException(message),
                "io" => new IOException(message),
                _ => new HttpRequestException(message),
            },
        };

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe("git failed\\u001B[31m\\u202E" + Environment.NewLine);
    }

    [Fact]
    public async Task Multiline_failures_keep_their_lines_and_escape_each_line()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var git = new CandidateGit { Failure = new InvalidOperationException("Command failed (128): git status\nfatal:\u001B[1m broken\r\n") };

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe("Command failed (128): git status" + Environment.NewLine + "fatal:\\u001B[1m broken" + Environment.NewLine);
    }
}
