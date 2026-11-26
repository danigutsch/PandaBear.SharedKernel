using System.Net;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises repository test taxonomy through the command line.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class TestTaxonomyCommandTests
{
    private const string ProjectPolicy = """
        {
          "version": 1,
          "target": { "id": "project-id", "owner": "owner", "number": 1 },
          "fields": [{ "name": "Coverage owner", "type": "TEXT" }],
          "issues": [{ "repository": "owner/repository", "number": 27 }]
        }
        """;
    private const string TargetResponse = """
        {"data":{"node":{"id":"project-id","number":1,"owner":{"login":"owner"}}}}
        """;
    private const string TextFieldResponse = """
        {"data":{"node":{"fields":{"nodes":[{"id":"coverage","name":"Coverage owner","dataType":"TEXT"}]}}}}
        """;
    private const string TextValueResponse = """
        {"data":{"node":{"fieldValues":{"nodes":[{"text":"Platform","field":{"id":"coverage","name":"Coverage owner"}}]}}}}
        """;

    [Fact]
    public async Task Accepts_an_explicit_canonical_suite_and_tracked_seam_register()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(0);
        output.ToString().ShouldContain("Test taxonomy is valid.", StringComparison.Ordinal);
        error.ToString().ShouldBeEmpty();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verifies_configured_project_membership_and_typed_fields_when_enabled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(
            handler, TextFieldResponse, TextValueResponse);
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(0, error.ToString());
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldContain("GitHub Project taxonomy is valid.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(5);
        handler.Requests.ShouldAllSatisfy(static request =>
        {
            request.PathAndQuery.ShouldBe("/graphql");
            using var payload = JsonDocument.Parse(request.Body.ShouldNotBeNull());
            payload.RootElement.GetProperty("query").GetString().ShouldNotBeNull().ShouldStartWith("query", StringComparison.Ordinal);
        });
        handler.Requests.ShouldContain(request =>
            request.Body?.Contains("\"number\":27", StringComparison.Ordinal) == true);
        handler.Requests.ShouldContain(request =>
            request.Body?.Contains("fieldValues", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("NUMBER", "\"number\":0", "", 0)]
    [InlineData("SINGLE_SELECT", "\"optionId\":\"enabled\"", ", \"options\":[{\"id\":\"enabled\"}]", 0)]
    [InlineData("SINGLE_SELECT", "\"optionId\":\"unknown\"", ", \"options\":[{\"id\":\"enabled\"}]", 1)]
    public async Task Requires_a_valid_numeric_or_select_field_value(string type, string value, string options, int expectedExit)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy.Replace("TEXT", type, StringComparison.Ordinal), ct);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(handler, $$"""
            { "data": { "node": { "fields": { "nodes": [
              { "id": "coverage", "name": "Coverage owner", "dataType": "{{type}}" {{options}} }
            ] } } } }
            """,
            $$"""
            { "data": { "node": { "fieldValues": { "nodes": [{ {{value}}, "field": { "id": "coverage" } }] } } } }
            """);
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(expectedExit, error.ToString());
        handler.Requests.Count.ShouldBe(5);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Reports_a_field_type_mismatch_in_strict_and_report_modes(bool report, int expectedExit)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, TargetResponse);
        handler.EnqueueJson(HttpStatusCode.OK, TextFieldResponse.Replace("\"TEXT\"", "\"NUMBER\"", StringComparison.Ordinal));
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        string[] args = report ? ["verify-test-taxonomy", "--github-project", "--report"] : ["verify-test-taxonomy", "--github-project"];

        // Act
        var result = await RepoConfigToolRunner.Run(args, output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(expectedExit);
        error.ToString().ShouldContain("Project field 'Coverage owner' must exist once with type TEXT.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false, "is missing from the configured GitHub Project.")]
    [InlineData(true, "must have one valid TEXT value for Project field 'Coverage owner'.")]
    public async Task Treats_equal_issue_numbers_in_separate_repositories_as_distinct(bool secondIssueIsMember, string diagnostic)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, TestTaxonomyTestRepository.PolicyWithAdditionalIssue(ProjectPolicy, "owner/other"), ct);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, TargetResponse);
        handler.EnqueueJson(HttpStatusCode.OK, TextFieldResponse);
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issue":{"id":"issue-one"}}}}""");
        const string secondItem = """{"id":"item-two","content":{"id":"issue-two"}}""";
        handler.EnqueueJson(HttpStatusCode.OK, $$"""
            { "data": { "node": { "items": {
              "nodes": [{{(secondIssueIsMember ? secondItem : string.Empty)}}],
              "pageInfo": { "hasNextPage": true, "endCursor": "one" }
            } } } }
            """);
        handler.EnqueueJson(HttpStatusCode.OK, """
            {"data":{"node":{"items":{"nodes":[{"id":"item-one","content":{"id":"issue-one"}}],"pageInfo":{"hasNextPage":false}}}}}
            """);
        handler.EnqueueJson(HttpStatusCode.OK, TextValueResponse);
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issue":{"id":"issue-two"}}}}""");
        if (secondIssueIsMember)
        {
            handler.EnqueueJson(HttpStatusCode.OK, """
                {"data":{"node":{"fieldValues":{"nodes":[{"number":1,"field":{"id":"coverage"}}]}}}}
                """);
        }

        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        var diagnostics = error.ToString();
        diagnostics.ShouldContain($"owner/other#27 {diagnostic}", StringComparison.Ordinal);
        diagnostics.ShouldNotContain("owner/repository#27", StringComparison.Ordinal);
        handler.Requests[2].Body.ShouldContain("\"repo\":\"repository\"", StringComparison.Ordinal);
        handler.Requests[4].Body.ShouldContain("\"after\":\"one\"", StringComparison.Ordinal);
        handler.Requests[6].Body.ShouldContain("\"repo\":\"other\"", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(secondIssueIsMember ? 8 : 7);
    }

    [Fact]
    public async Task Rejects_duplicate_configured_issue_identity_before_a_remote_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, TestTaxonomyTestRepository.PolicyWithAdditionalIssue(ProjectPolicy, "Owner/Repository"), ct);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project", "--report"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("contains a duplicate issue reference", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 2", "must use version 1.")]
    [InlineData("\"number\": 1", "\"number\": 0", "requires a Project id, owner, and positive number.")]
    [InlineData("\"id\": \"project-id\"", "\"id\": \"\"", "requires a Project id, owner, and positive number.")]
    [InlineData("\"owner\": \"owner\"", "\"owner\": \" \"", "requires a Project id, owner, and positive number.")]
    [InlineData("\"type\": \"TEXT\"", "\"type\": \"DATE\"", "requires named fields of type TEXT, NUMBER, or SINGLE_SELECT.")]
    [InlineData("\"name\": \"Coverage owner\"", "\"name\": \" \"", "requires named fields of type TEXT, NUMBER, or SINGLE_SELECT.")]
    [InlineData("{ \"name\": \"Coverage owner\", \"type\": \"TEXT\" }",
        "{ \"name\": \"Coverage owner\", \"type\": \"TEXT\" }, { \"name\": \"Coverage owner\", \"type\": \"NUMBER\" }",
        "contains a duplicate field name.")]
    [InlineData("\"repository\": \"owner/repository\"", "\"repository\": \"invalid\"", "requires issue references with an owner/name repository and positive number.")]
    public async Task Rejects_malformed_project_policy_without_remote_reads(string original, string replacement, string diagnostic)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy.Replace(original, replacement, StringComparison.Ordinal), ct);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project", "--report"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain(diagnostic, StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_different_references_that_resolve_to_the_same_issue()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, TestTaxonomyTestRepository.PolicyWithAdditionalIssue(ProjectPolicy, "owner/other"), ct);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(handler, TextFieldResponse, TextValueResponse);
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issue":{"id":"issue-id"}}}}""");
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("owner/other#27 resolved to a duplicate Project issue identity.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Remote_transport_or_response_failure_stays_unverified_in_report_mode(bool transportFailure)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = transportFailure
            ? TestHttpMessageHandler.FromException(new HttpRequestException("private request detail"))
            : new TestHttpMessageHandler(HttpResponseFactory.Json("private response detail", HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project", "--report"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldBe("repo: GitHub Project taxonomy metadata could not be read. Check the configured Project and read access.\n");
        output.ToString().ShouldNotContain("GitHub Project taxonomy is valid.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FORBIDDEN", "GitHub Project request failed (FORBIDDEN).")]
    [InlineData("INSUFFICIENT_SCOPES", "GitHub Project request failed (INSUFFICIENT_SCOPES).")]
    [InlineData("untrusted-secret", "GitHub Project request failed.")]
    public async Task Rejects_partial_remote_data_and_sanitizes_the_diagnostic(string errorType, string diagnostic)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""
            {
              "data": { "node": { "id": "project-id", "number": 1, "owner": { "login": "owner" } } },
              "errors": [{ "type": "{{errorType}}", "message": "untrusted-secret" }]
            }
            """);
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project", "--report"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldBe($"repo: {diagnostic}\n");
        output.ToString().ShouldNotContain("GitHub Project taxonomy is valid.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stops_when_membership_has_a_missing_or_repeated_cursor(bool repeat)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, TargetResponse);
        handler.EnqueueJson(HttpStatusCode.OK, TextFieldResponse);
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issue":{"id":"issue-id"}}}}""");
        const string repeatedPage = """
            {"data":{"node":{"items":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"one"}}}}}
            """;
        handler.EnqueueJson(HttpStatusCode.OK, repeat ? repeatedPage : repeatedPage.Replace("\"one\"", "null", StringComparison.Ordinal));
        if (repeat)
        {
            handler.EnqueueJson(HttpStatusCode.OK, repeatedPage);
        }

        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("GitHub Project item pagination did not advance.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(repeat ? 5 : 4);
    }

    [Theory]
    [InlineData("other-item", "issue-id")]
    [InlineData("item-id", "other-issue")]
    public async Task Rejects_conflicting_remote_issue_and_item_identities(string secondItem, string secondIssue)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, TargetResponse);
        handler.EnqueueJson(HttpStatusCode.OK, TextFieldResponse);
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issue":{"id":"issue-id"}}}}""");
        handler.EnqueueJson(HttpStatusCode.OK, $$"""
            {"data":{"node":{"items":{"nodes":[
              {"id":"item-id","content":{"id":"issue-id"} },
              {"id":"{{secondItem}}","content":{"id":"{{secondIssue}}"} }
            ],"pageInfo":{"hasNextPage":false} } } } }
            """);
        using var httpClient = new HttpClient(handler);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(
            ["verify-test-taxonomy", "--github-project"], output, error, repository.RootPath, httpClient, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("GitHub Project returned conflicting issue and item identities.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Preserves_caller_cancellation_during_the_project_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        await TestTaxonomyTestRepository.StageProjectPolicy(repository, ProjectPolicy, ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var handler = new DelegateHttpMessageHandler(async (_, requestToken) =>
        {
            await cancellation.CancelAsync();
            requestToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);

        // Act
        Func<Task> verify = async () => _ = await GitHubProjectTaxonomyVerifier.Verify(repository.RootPath, connection, cancellation.Token);
        var exception = await verify.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Reports_unregistered_projects_even_when_their_names_look_canonical()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create("", ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("tests/Widget.Tests.Unit/Widget.Tests.Unit.csproj: Declare this test project", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_invalid_seam_and_unregistered_sut_and_absent_register()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entry = TestTaxonomyTestRepository.CanonicalSuite
            .Replace("src/Widget/Widget.csproj", "src/Missing/Missing.csproj", StringComparison.Ordinal)
            .Replace("\"Unit\"", "\"Behavior\"", StringComparison.Ordinal)
            .Replace("tests/SEAMS.md", "tests/Missing.md", StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(entry, ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);
        var diagnostics = error.ToString();

        // Assert
        result.ShouldBe(0);
        diagnostics.ShouldContain("test type 'Behavior' is unsupported", StringComparison.Ordinal);
        diagnostics.ShouldContain("SUT 'src/Missing/Missing.csproj' is not registered", StringComparison.Ordinal);
        diagnostics.ShouldContain("seam register 'tests/Missing.md' is not a tracked Markdown file", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Widget.LegacyTests", "", 1)]
    [InlineData("Widget.LegacyTests", ", \"migrationException\": \"Retains the legacy runner name until its callers move.\"", 0)]
    public async Task Requires_a_migration_reason_for_legacy_names(string projectName, string exception, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entry = TestTaxonomyTestRepository.CanonicalSuite
            .Replace("Widget.Tests.Unit", projectName, StringComparison.Ordinal)
            .Replace("\n}", exception + "\n}", StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(entry, ct, projectName);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(expected);
        if (expected != 0)
        {
            error.ToString().ShouldContain("Rename to 'Widget.Tests.Unit'", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Reports_missing_production_coverage_entries_and_missing_required_types()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create("", ct);
        repository.WriteText("src/Other/Other.csproj", "<Project />");
        await repository.Stage("src/Other/Other.csproj", ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);
        var diagnostics = error.ToString();

        // Assert
        result.ShouldBe(1);
        diagnostics.ShouldContain("src/Other/Other.csproj: Register this production SUT", StringComparison.Ordinal);
        diagnostics.ShouldContain("src/Widget/Widget.csproj: Declare a suite for required test type 'Unit'.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_in_report_mode_when_the_inventory_is_untracked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("Track the repository test taxonomy", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 2", "must be a version 1 inventory", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": []", "must declare separate, root-relative", 1)]
    [InlineData("\"testRoots\": [\"tests\"]", "\"testRoots\": []", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\" \"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src\\\\child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"../src\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src/./child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src//child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"tests/child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"suts\": [", "\"suts\": [null,", "Remove null entries", 0)]
    [InlineData("\"suites\": [", "\"suites\": [null,", "Remove null entries", 0)]
    [InlineData("\"path\": \"src/Widget/Widget.csproj\"", "\"path\": \"src/Unknown.csproj\"", "production SUT 'src/Unknown.csproj'", 0)]
    [InlineData("\"directOwner\": \"Widget\"", "\"directOwner\": \"\"", "Declare the direct owner", 0)]
    [InlineData("\"requiredTypes\": [\"Unit\"]", "\"requiredTypes\": [\"Behavior\"]", "Required test type 'Behavior' is unsupported", 0)]
    [InlineData("\"type\": \"Unit\"", "\"type\": \"Behavior\"", "test type 'Behavior' is unsupported", 0)]
    [InlineData("\"seams\": [", "\"seams\": [null,", "Declare at least one seam", 0)]
    [InlineData("tests/SEAMS.md", "tests/SEAMS.txt", "is not a tracked Markdown file", 0)]
    [InlineData("\"dependency\": \"in-process\"", "\"dependency\": \"\"", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("\"lane\": \"fast\"", "\"lane\": \"\"", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("[\"example\"]", "[]", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("[\"example\"]", "[\" \"]", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"tests/Support.csproj\"]", "must be a tracked project", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"tests/SEAMS.md\"]", "must be a tracked project", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"src/Widget/Widget.csproj\"]", "must be a tracked project", 0)]
    [InlineData(
        "\"supportProjects\": []",
        "\"supportProjects\": [\"tests/Widget.Tests.Unit/Widget.Tests.Unit.csproj\"]",
        "Remove the duplicate test support project declaration",
        0)]
    public async Task Reports_inventory_shape_and_ownership_problems(string original, string replacement, string diagnostic, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        const string inventoryPath = ".config/test-taxonomy.json";
        var inventory = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, inventoryPath), ct);
        repository.WriteText(inventoryPath, inventory.Replace(original, replacement, StringComparison.Ordinal));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(expected);
        error.ToString().ShouldContain(diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_seams_need_a_single_declaration_and_a_valid_project_shape()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var suite = TestTaxonomyTestRepository.CanonicalSuite.Replace(
            "\"seams\": [",
            """
            "seams": [
              {"sut":"src/Widget/Widget.csproj","type":"Unit","dependency":"in-process","capabilities":["duplicate"],"lane":"fast"},
            """,
            StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(suite, ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        var diagnostics = error.ToString();
        diagnostics.ShouldContain("Merge duplicate seam declarations", StringComparison.Ordinal);
        diagnostics.ShouldContain("Split the project by declared SUT and test type", StringComparison.Ordinal);
    }
}
