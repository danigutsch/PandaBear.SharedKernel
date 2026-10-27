using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>dependency submit</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class DependencySubmitCommandTests
{
    private const string SnapshotPath = "TestResults/dependency-snapshot.json";
    private const string Snapshot = """{"version":0,"sha":"0123456789abcdef0123456789abcdef01234567","ref":"refs/heads/main","manifests":{}}""";
    private const string Endpoint = "https://api.github.com/repos/owner/repository/dependency-graph/snapshots";

    [Fact]
    public async Task Submit_previews_the_snapshot_and_endpoint_without_a_request()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        output.ToString().ShouldBe(
            $"preview: dependency submission would send {SnapshotPath} ({Encoding.UTF8.GetByteCount(Snapshot).ToString(CultureInfo.InvariantCulture)} bytes) to {Endpoint}." + Environment.NewLine,
            "The relative snapshot path resolves against --root, not the working directory.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_preview_does_not_read_a_token()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var ghToken = new EnvironmentVariableScope("GH_TOKEN", null);
        using var gitHubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        output.ToString().ShouldStartWith("preview: dependency submission would send", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_with_force_posts_the_exact_snapshot_bytes_to_the_repository_dependency_graph()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, """{"id":12345,"created_at":"2026-10-24T00:00:00Z","message":"Dependency results for the repo have been successfully updated.","result":"SUCCESS"}""");
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);
        var request = handler.Requests.ShouldHaveSingleItem();

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        output.ToString().ShouldBe("Dependency snapshot submitted to owner/repository: SUCCESS." + Environment.NewLine);
        request.Method.ShouldBe(HttpMethod.Post);
        request.Message.RequestUri.ShouldBe(new Uri(Endpoint));
        request.Message.Headers.GetValues("X-GitHub-Api-Version").ShouldBe(["2022-11-28"]);
        request.Message.Content.ShouldNotBeNull().Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        request.Body.ShouldBe(Snapshot);
    }

    [Fact]
    public async Task Submit_with_force_requires_a_token()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var ghToken = new EnvironmentVariableScope("GH_TOKEN", null);
        using var gitHubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub dependency submission." + Environment.NewLine);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/repository/extra")]
    [InlineData("owner/..")]
    [InlineData("owner/.")]
    [InlineData("-owner/repository")]
    [InlineData("owner/repo sitory")]
    [InlineData("owner/repository\n")]
    [InlineData("owner/repository?x=1")]
    public async Task Submit_rejects_a_repository_that_is_not_owner_and_name_before_any_request(string repository)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", repository, "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe("sharedkernel-repo: --repository must name a GitHub repository as owner/name." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[]", "sharedkernel-repo: TestResults/dependency-snapshot.json must contain a JSON object.")]
    [InlineData("{", "sharedkernel-repo: TestResults/dependency-snapshot.json: Invalid JSON at line 1, byte 2: Expected depth to be zero at the end of the JSON payload.")]
    [InlineData("{} // comment", "sharedkernel-repo: TestResults/dependency-snapshot.json: Invalid JSON at line 1, byte 4:")]
    public async Task Submit_requires_a_json_object_snapshot_before_any_request(string snapshot, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, snapshot);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldStartWith(expectedError, StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_requires_the_snapshot_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe($"sharedkernel-repo: {SnapshotPath} does not exist." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_rejects_a_snapshot_larger_than_the_snapshot_command_writes()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        await using (var stream = File.Open(Path.Combine(workspace.RootPath, SnapshotPath), FileMode.Create))
        {
            stream.SetLength((32 * 1024 * 1024) + 3);
        }

        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe($"sharedkernel-repo: {SnapshotPath} exceeds the 32 MiB snapshot size limit." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Submit_reports_a_rejected_request_without_untrusted_response_details(HttpStatusCode statusCode)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(GitHubSyncTestResponseFactory.UntrustedError(statusCode));
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe($"sharedkernel-repo: GitHub dependency submission failed with HTTP {((int)statusCode).ToString(CultureInfo.InvariantCulture)}." + Environment.NewLine);
    }

    [Fact]
    public async Task Submit_fails_when_github_reports_the_snapshot_as_invalid()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, """{"id":12345,"created_at":"2026-10-24T00:00:00Z","message":"ghp_untrusted_message","result":"INVALID"}""");
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: GitHub reported the dependency snapshot as invalid." + Environment.NewLine);
    }

    [Fact]
    public async Task Submit_reports_a_transport_failure_without_request_details()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("ghp_transport_detail"));
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe("sharedkernel-repo: GitHub dependency submission request failed." + Environment.NewLine);
    }

    [Fact]
    public async Task Submit_times_out_a_request_that_github_does_not_answer()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var options = new DependencySubmitOptions(workspace.RootPath, SnapshotPath, "owner/repository", Force: true);

        // Act
        var action = (Func<Task>)(async () => await DependencySubmitCommand.Run(options, httpClient, timeProvider, output, TestContext.Current.CancellationToken));
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub dependency submission timed out after 30 seconds.");
        output.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Submit_bounds_the_response_it_reads()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new ByteArrayContent(new byte[(1024 * 1024) + 1]),
        });
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", "--snapshot", SnapshotPath, "--repository", "owner/repository", "--force", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            httpClient,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe("sharedkernel-repo: GitHub dependency submission response exceeded the allowed size." + Environment.NewLine);
    }

    [Theory]
    [InlineData("--snapshot", "")]
    [InlineData("--snapshot", " ")]
    public async Task Submit_rejects_a_blank_snapshot_path(string option, string value)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", option, value, "--repository", "owner/repository", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("--snapshot requires a file path.", StringComparison.Ordinal);
        output.ToString().ShouldNotContain("preview:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--repository", "owner/repository")]
    [InlineData("--snapshot", SnapshotPath)]
    public async Task Submit_requires_the_snapshot_and_the_repository(string option, string value)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["dependency", "submit", option, value, "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("is required.", StringComparison.Ordinal);
        output.ToString().ShouldNotContain("preview:", StringComparison.Ordinal);
    }
}
