using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

using static PandaBear.RepoConfig.Tests.GitHubLabelSyncTestOperations;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>github sync-labels</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class GitHubLabelSyncCommandTests
{
    private const string LabelsPath = "/repos/owner/repository/labels";

    private const string Configuration = """
        {
          "version": 1,
          "labels": [
            { "name": "ci", "color": "1d76db", "description": "Continuous integration" },
            { "name": "type: epic", "color": "6f42c1", "description": "Parent issue" },
            { "name": "area: api", "color": "0e8a16", "description": "HTTP API contracts" },
            { "name": "dependencies", "color": "0366d6", "description": "" }
          ]
        }
        """;

    // "ci" is current (color case and a null description are equivalent), "Type: Epic" differs in name case and color,
    // "area: api" is missing, "dependencies" is current, and "obsolete" is not configured.
    private const string RemoteLabels = """
        [
          { "id": 1, "name": "ci", "color": "1D76DB", "description": "Continuous integration", "default": false },
          { "id": 2, "name": "Type: Epic", "color": "000000", "description": "Parent issue", "default": false },
          { "id": 3, "name": "dependencies", "color": "0366d6", "description": null, "default": false },
          { "id": 4, "name": "obsolete", "color": "ffffff", "description": "Not configured", "default": false }
        ]
        """;

    /// <summary>Verifies that a preview lists the labels to create and update without sending any write.</summary>
    [Fact]
    public async Task Sync_labels_previews_creates_and_updates_without_writing()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, RemoteLabels);

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "preview: would update label type: epic." + Environment.NewLine
            + "preview: would create label area: api." + Environment.NewLine
            + "preview: owner/repository would get 1 created, 1 updated, and 2 unchanged labels; --force writes them." + Environment.NewLine);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.PathAndQuery.ShouldBe(LabelsPath + "?per_page=100&page=1");
    }

    /// <summary>Verifies that force creates missing labels, updates changed ones, and deletes nothing.</summary>
    [Fact]
    public async Task Sync_labels_with_force_creates_missing_and_updates_changed_labels_without_deleting()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, RemoteLabels);
        handler.EnqueueJson(HttpStatusCode.OK, """{"name":"type: epic"}""");
        handler.EnqueueJson(HttpStatusCode.Created, """{"name":"area: api"}""");

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: true);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "Updated label type: epic." + Environment.NewLine
            + "Created label area: api." + Environment.NewLine
            + "owner/repository: 1 created, 1 updated, and 2 unchanged labels." + Environment.NewLine);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Delete);
        var update = handler.Requests[1];
        update.Method.ShouldBe(HttpMethod.Patch);
        update.Message.RequestUri!.AbsoluteUri.ShouldBe("https://api.github.com/repos/owner/repository/labels/Type%3A%20Epic");
        using var updateBody = JsonDocument.Parse(update.Body.ShouldNotBeNull());
        updateBody.RootElement.GetProperty("new_name").GetString().ShouldBe("type: epic");
        updateBody.RootElement.GetProperty("color").GetString().ShouldBe("6f42c1");
        updateBody.RootElement.GetProperty("description").GetString().ShouldBe("Parent issue");
        var create = handler.Requests[2];
        create.Method.ShouldBe(HttpMethod.Post);
        create.PathAndQuery.ShouldBe(LabelsPath);
        using var createBody = JsonDocument.Parse(create.Body.ShouldNotBeNull());
        createBody.RootElement.GetProperty("name").GetString().ShouldBe("area: api");
        createBody.RootElement.GetProperty("color").GetString().ShouldBe("0e8a16");
        createBody.RootElement.GetProperty("description").GetString().ShouldBe("HTTP API contracts");
    }

    /// <summary>Verifies that the command follows pagination to read all remote labels.</summary>
    [Fact]
    public async Task Sync_labels_reads_every_page_of_remote_labels()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        var firstPage = string.Join(',', Enumerable.Range(1, 100).Select(index =>
            string.Create(CultureInfo.InvariantCulture, $$"""{"name":"other-{{index}}","color":"ffffff","description":null}""")));
        handler.EnqueueJson(HttpStatusCode.OK, "[" + firstPage + "]");
        handler.EnqueueJson(HttpStatusCode.OK, RemoteLabels);

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldEndWith("1 created, 1 updated, and 2 unchanged labels; --force writes them." + Environment.NewLine, StringComparison.Ordinal);
        handler.Requests.Select(request => request.PathAndQuery).ShouldBe(
        [
            LabelsPath + "?per_page=100&page=1",
            LabelsPath + "?per_page=100&page=2",
        ]);
    }

    /// <summary>Verifies that an invalid repository value fails before the configuration is read.</summary>
    [Fact]
    public async Task Sync_labels_rejects_an_invalid_repository_before_reading_configuration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: true, repository: "owner/repository/extra");

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldBe("sharedkernel-repo: --repository must name a GitHub repository as owner/name." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that the command fails when the configuration has no labels section.</summary>
    [Fact]
    public async Task Sync_labels_requires_the_labels_section()
    {
        // Arrange
        using var workspace = CreateWorkspace("""{"version":1}""");
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json has no labels section." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that the command rejects a label with an invalid value.</summary>
    [Theory]
    [InlineData("[]", "$.labels must name at least one label.")]
    [InlineData("""[{"name":"","color":"ffffff","description":""}]""", "$.labels[0].name must be 1 to 50 characters without control characters or surrounding white space.")]
    [InlineData("""[{"name":" ci","color":"ffffff","description":""}]""", "$.labels[0].name must be 1 to 50 characters without control characters or surrounding white space.")]
    [InlineData("""[{"name":"c\ti","color":"ffffff","description":""}]""", "$.labels[0].name must be 1 to 50 characters without control characters or surrounding white space.")]
    [InlineData("""[{"name":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","color":"ffffff","description":""}]""", "$.labels[0].name must be 1 to 50 characters without control characters or surrounding white space.")]
    [InlineData("""[{"name":"ci","color":"ffffff","description":""},{"name":"CI","color":"ffffff","description":""}]""", "$.labels[1].name repeats a label name that differs only in case.")]
    [InlineData("""[{"name":"ci","color":"#ffffff","description":""}]""", "$.labels[0].color must be six hexadecimal digits without '#'.")]
    [InlineData("""[{"name":"ci","color":"fffff","description":""}]""", "$.labels[0].color must be six hexadecimal digits without '#'.")]
    [InlineData("""[{"name":"ci","color":"gggggg","description":""}]""", "$.labels[0].color must be six hexadecimal digits without '#'.")]
    [InlineData("""[{"name":"ci","color":"ffffff","description":"line\nbreak"}]""", "$.labels[0].description must be at most 100 characters without control characters.")]
    public async Task Sync_labels_rejects_an_invalid_label(string labels, string expectedMessage)
    {
        // Arrange
        using var workspace = CreateWorkspace($$"""{"version":1,"labels":{{labels}}}""");
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe($"sharedkernel-repo: .config/sharedkernel-repo.json: {expectedMessage}" + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that the command rejects a label description longer than GitHub allows.</summary>
    [Fact]
    public async Task Sync_labels_rejects_a_description_longer_than_github_allows()
    {
        // Arrange
        using var workspace = CreateWorkspace($$"""{"version":1,"labels":[{"name":"ci","color":"ffffff","description":"{{new string('d', 101)}}"}]}""");
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json: $.labels[0].description must be at most 100 characters without control characters." + Environment.NewLine);
    }

    /// <summary>Verifies that the command rejects a label with a missing required property or an unknown one.</summary>
    [Theory]
    [InlineData("""[{"name":"ci","color":"ffffff"}]""", "$.labels[0]")]
    [InlineData("""[{"name":"ci","color":"ffffff","description":null}]""", "$.labels[0].description")]
    [InlineData("""[{"name":"ci","color":"ffffff","description":"","default":true}]""", "$.labels[0].default")]
    public async Task Sync_labels_rejects_a_missing_or_unknown_label_property(string labels, string expectedPath)
    {
        // Arrange
        using var workspace = CreateWorkspace($$"""{"version":1,"labels":{{labels}}}""");
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldStartWith($"sharedkernel-repo: .config/sharedkernel-repo.json is not valid at {expectedPath} (line 1).", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that the command fails for a missing token before any request to GitHub.</summary>
    [Fact]
    public async Task Sync_labels_requires_a_token_before_reading_github()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var ghToken = new EnvironmentVariableScope("GH_TOKEN", null);
        using var githubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["github", "sync-labels", "--repository", "owner/repository", "--root", workspace.RootPath],
            output,
            error,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe("sharedkernel-repo: Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub label sync." + Environment.NewLine);
    }

    /// <summary>Verifies that a failed read reports the status without echoing the response content.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "[]", "GitHub label sync failed with HTTP 403.")]
    [InlineData(HttpStatusCode.OK, "{}", "GitHub label sync returned an invalid label response.")]
    [InlineData(HttpStatusCode.OK, """[{"name":"ci","color":1,"description":null}]""", "GitHub label sync returned an invalid label response.")]
    [InlineData(HttpStatusCode.OK, "ghp_untrusted invalid JSON", "GitHub label sync returned an invalid label response.")]
    public async Task Sync_labels_reports_a_failed_read_without_echoing_the_response(HttpStatusCode status, string body, string expectedMessage)
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(status, body);

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: true);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldBe($"sharedkernel-repo: {expectedMessage}" + Environment.NewLine);
        handler.Requests.ShouldHaveSingleItem();
    }

    /// <summary>Verifies that a failed write names the label it was writing.</summary>
    [Fact]
    public async Task Sync_labels_names_the_label_whose_write_failed()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, RemoteLabels);
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.UnprocessableEntity, """{"message":"ghp_untrusted"}""");

        // Act
        var (exitCode, output, error) = await Run(workspace, handler, force: true);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe("Updated label type: epic." + Environment.NewLine);
        error.ShouldBe("sharedkernel-repo: GitHub label sync failed with HTTP 422 for label area: api. Rerun the sync after fixing the cause." + Environment.NewLine);
    }

    /// <summary>Verifies that the command limits how many response bytes it reads.</summary>
    [Fact]
    public async Task Sync_labels_bounds_the_response_it_reads()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("[" + new string(' ', 1024 * 1024) + "]")),
        });

        // Act
        var (exitCode, _, error) = await Run(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe("sharedkernel-repo: GitHub label sync response exceeded the allowed size." + Environment.NewLine);
    }

    /// <summary>Verifies that the command times out a request that GitHub does not answer.</summary>
    [Fact]
    public async Task Sync_labels_times_out_a_request_that_github_does_not_answer()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var action = (Func<Task>)(async () => await GitHubLabelSyncCommand.Run(
            workspace.RootPath, "owner/repository", force: true, httpClient, timeProvider, output, TestContext.Current.CancellationToken));
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub label sync timed out after 30 seconds.");
        output.ToString().ShouldBe(string.Empty);
    }
}
