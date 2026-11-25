using System.Net;
using System.Text.Json;

using static SharedKernel.RepoConfig.Tests.ForgejoLabelSyncTestOperations;
using static SharedKernel.RepoConfig.Tests.GitHubLabelSyncTestOperations;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests the <c>github sync-labels --forgejo</c> command, which synchronizes the labels of a Forgejo repository.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class ForgejoLabelSyncCommandTests
{
    private const string LabelsPath = "/api/v1/repos/owner/repository/labels";

    private const string Configuration = """
        {
          "version": 1,
          "labels": [
            { "name": "ci", "color": "1d76db", "description": "Continuous integration" },
            { "name": "type: epic", "color": "6f42c1", "description": "Parent issue" },
            { "name": "area: api", "color": "0e8a16", "description": "HTTP API contracts" }
          ]
        }
        """;

    // "ci" is current, "Type: Epic" differs in name case and color, "area: api" is missing, and "obsolete" is not configured.
    private const string RemoteLabels = """
        [
          { "id": 1, "name": "ci", "color": "1D76DB", "description": "Continuous integration", "exclusive": false, "is_archived": false },
          { "id": 2, "name": "Type: Epic", "color": "000000", "description": "Parent issue", "exclusive": false, "is_archived": false },
          { "id": 4, "name": "obsolete", "color": "ffffff", "description": "", "exclusive": false, "is_archived": false }
        ]
        """;

    [Fact]
    public async Task A_preview_reads_a_public_repository_without_a_token_and_writes_nothing()
    {
        // Arrange
        using var token = new EnvironmentVariableScope("FORGEJO_TOKEN", null);
        using var gitHubToken = new EnvironmentVariableScope("GH_TOKEN", "github-secret");
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(CountedLabels(RemoteLabels, 3));

        // Act
        var (exitCode, output, error) = await RunForgejo(workspace, handler, force: false);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "preview: would update label type: epic.\n"
            + "preview: would create label area: api.\n"
            + "preview: owner/repository would get 1 created, 1 updated, and 1 unchanged labels; --force writes them.\n");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.Message.RequestUri!.AbsoluteUri.ShouldBe("https://codeberg.example" + LabelsPath + "?limit=50&page=1");
        request.Message.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task Force_writes_with_the_forgejo_token_and_updates_by_label_ID()
    {
        // Arrange
        using var token = new EnvironmentVariableScope("FORGEJO_TOKEN", "forgejo-secret");
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(CountedLabels(RemoteLabels, 3));
        handler.EnqueueJson(HttpStatusCode.OK, """{ "id": 2, "name": "type: epic", "color": "6f42c1" }""");
        handler.EnqueueJson(HttpStatusCode.Created, """{ "id": 5, "name": "area: api", "color": "0e8a16" }""");

        // Act
        var (exitCode, output, error) = await RunForgejo(workspace, handler, force: true);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "Updated label type: epic.\n"
            + "Created label area: api.\n"
            + "owner/repository: 1 created, 1 updated, and 1 unchanged labels.\n");
        handler.Requests.ShouldAllSatisfy(static request =>
            request.Message.Headers.Authorization!.ToString().ShouldBe("token forgejo-secret"));
        var update = handler.Requests[1];
        update.Method.ShouldBe(HttpMethod.Patch);
        update.PathAndQuery.ShouldBe(LabelsPath + "/2");
        using var updateBody = JsonDocument.Parse(update.Body.ShouldNotBeNull());
        updateBody.RootElement.GetProperty("name").GetString().ShouldBe("type: epic");
        updateBody.RootElement.GetProperty("color").GetString().ShouldBe("#6f42c1");
        var create = handler.Requests[2];
        create.Method.ShouldBe(HttpMethod.Post);
        create.PathAndQuery.ShouldBe(LabelsPath);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task Force_without_a_forgejo_token_sends_no_request()
    {
        // Arrange
        using var token = new EnvironmentVariableScope("FORGEJO_TOKEN", null);
        using var gitHubToken = new EnvironmentVariableScope("GITHUB_TOKEN", "github-secret");
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, output, error) = await RunForgejo(workspace, handler, force: true);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldBe("repo: Set FORGEJO_TOKEN to a token that may write the labels of owner/repository; --force sent no request.\n");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/name/extra")]
    [InlineData("owner/..")]
    public async Task A_repository_that_is_not_owner_slash_name_is_a_parse_error_before_any_request(string repository)
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await RunForgejo(workspace, handler, force: false, repository: repository);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("--repository requires owner/name: ASCII letters, digits, '-', '.', and '_' in each part, and neither part '.' nor '..'.", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://codeberg.example")]
    [InlineData("https://codeberg.example/?a=1")]
    [InlineData("codeberg.example")]
    public async Task A_forgejo_address_that_is_not_plain_https_is_rejected(string server)
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration);
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await RunForgejo(workspace, handler, force: false, server);

        // Assert
        exitCode.ShouldNotBe(0);
        error.ShouldContain("--forgejo requires an absolute HTTPS address without user information, a query, or a fragment, such as https://codeberg.org.", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }
}
