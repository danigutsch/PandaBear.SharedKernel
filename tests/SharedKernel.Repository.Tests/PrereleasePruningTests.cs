using System.Globalization;
using System.Net;

namespace SharedKernel.Repository.Tests;

/// <summary>Tests <c>prune-prereleases</c> against an in-memory GitHub releases API, so no test reaches the network.</summary>
public sealed class PrereleasePruningTests
{
    private static readonly DateTimeOffset Start = FakeGitHubReleasesApi.Start;

    [Fact]
    public async Task Preview_keeps_the_newest_prereleases_lists_the_rest_and_deletes_nothing()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(5);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "2"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("keep    v0.1.0-alpha.5.1", StringComparison.Ordinal);
        result.Output.ShouldContain("keep    v0.1.0-alpha.4.1", StringComparison.Ordinal);
        result.Output.ShouldContain("delete  v0.1.0-alpha.3.1", StringComparison.Ordinal);
        result.Output.ShouldContain("delete  v0.1.0-alpha.1.1", StringComparison.Ordinal);
        result.Output.ShouldContain("preview: would delete 3 prereleases; --force deletes them. Tags stay.", StringComparison.Ordinal);
        api.Requests.ShouldAllSatisfy(static request => request.Method.ShouldBe(HttpMethod.Get));
        api.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Prereleases_are_ordered_by_creation_date_not_by_list_order()
    {
        // Arrange: the oldest release is added last, so the API lists it first.
        using var api = new FakeGitHubReleasesApi();
        api.Add("v0.1.0-alpha.2.1", Start.AddDays(2));
        api.Add("v0.1.0-alpha.3.1", Start.AddDays(3));
        api.Add("v0.1.0-alpha.1.1", Start.AddDays(1));

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        api.Deleted.ShouldBe([api.IdOf("v0.1.0-alpha.2.1"), api.IdOf("v0.1.0-alpha.1.1")]);
    }

    [Fact]
    public async Task Protected_versions_are_kept_beyond_the_newest_and_do_not_count_toward_them()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(6);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "2", "--protect", "0.1.0-alpha.2.1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("keep    v0.1.0-alpha.2.1", StringComparison.Ordinal);
        api.Deleted.ShouldBe([api.IdOf("v0.1.0-alpha.4.1"), api.IdOf("v0.1.0-alpha.3.1"), api.IdOf("v0.1.0-alpha.1.1")],
            "The two newest stay, and the protected release is a third that does not displace either.");
    }

    [Fact]
    public async Task Stable_releases_and_drafts_are_never_deleted()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.Add("v0.0.9", Start, prerelease: false);
        api.Add("v0.1.0-alpha.0.1", Start.AddDays(1), draft: true);
        api.Add("v0.1.0-draft", Start.AddDays(2), prerelease: false, draft: true);
        api.AddPrereleases(3);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        api.Deleted.ShouldBe([api.IdOf("v0.1.0-alpha.2.1"), api.IdOf("v0.1.0-alpha.1.1")]);
        result.Output.ShouldContain("Ignored 1 stable release and 2 drafts.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Force_deletes_only_the_expected_releases_through_the_release_endpoint_and_never_a_tag()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(4);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "2", "--force"], PrereleasePruningRuns.Token);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var deletes = api.Requests.Where(static request => request.Method == HttpMethod.Delete).Select(static request => request.Uri.AbsoluteUri).ToArray();
        deletes.ShouldBe([
            FakeGitHubReleasesApi.ReleasesUrl + "/" + api.IdOf("v0.1.0-alpha.2.1").ToString(CultureInfo.InvariantCulture),
            FakeGitHubReleasesApi.ReleasesUrl + "/" + api.IdOf("v0.1.0-alpha.1.1").ToString(CultureInfo.InvariantCulture),
        ]);
        api.Requests.ShouldAllSatisfy(static request =>
        {
            request.Uri.AbsolutePath.ShouldNotContain("/git/", StringComparison.Ordinal, "Tags stay; no ref endpoint is called.");
            request.Uri.AbsolutePath.ShouldNotContain("/tags", StringComparison.Ordinal);
            request.Authorization.ShouldBe("Bearer " + PrereleasePruningRuns.Token);
            request.UserAgent.ShouldStartWith("sharedkernel-ci/", StringComparison.Ordinal);
            request.ApiVersion.ShouldBe("2026-03-10");
        });
        result.Output.ShouldContain("deleted v0.1.0-alpha.2.1", StringComparison.Ordinal);
        result.Output.ShouldContain("Deleted 2 prereleases; their tags stay.", StringComparison.Ordinal);
        var printed = result.Output + result.Error;
        printed.ShouldNotContain(PrereleasePruningRuns.Token, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_page_is_read_before_anything_is_deleted()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(103);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "100", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        api.Requests.Where(static request => request.Method == HttpMethod.Get).Select(static request => request.Uri.Query).ShouldBe(
            ["?per_page=100&page=1", "?per_page=100&page=2"]);
        api.Deleted.ShouldBe([api.IdOf("v0.1.0-alpha.3.1"), api.IdOf("v0.1.0-alpha.2.1"), api.IdOf("v0.1.0-alpha.1.1")],
            "The oldest releases are on the second page.");
        api.Requests.TakeWhile(static request => request.Method == HttpMethod.Get).Count().ShouldBe(2);
    }

    [Fact]
    public async Task A_failed_deletion_stops_every_later_deletion_and_reports_what_was_deleted()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(5);
        api.FailDelete(api.IdOf("v0.1.0-alpha.3.1"), HttpStatusCode.InternalServerError);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        api.Deleted.ShouldBe([api.IdOf("v0.1.0-alpha.4.1")]);
        api.Requests.Count(static request => request.Method == HttpMethod.Delete).ShouldBe(2, "Nothing is sent after the failure.");
        result.Error.ShouldContain("returned 500 InternalServerError", StringComparison.Ordinal);
        result.Error.ShouldContain("Deleted before the failure: v0.1.0-alpha.4.1.", StringComparison.Ordinal);
        result.Error.ShouldContain("No further release was deleted.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, HttpStatusCode.Unauthorized)]
    [InlineData(2, HttpStatusCode.InternalServerError)]
    public async Task A_failed_list_page_fails_before_any_deletion(int page, HttpStatusCode status)
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(150);
        api.FailPage(page, status);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(string.Create(CultureInfo.InvariantCulture, $"returned {(int)status} {status}"), StringComparison.Ordinal);
        api.Requests.ShouldAllSatisfy(static request => request.Method.ShouldBe(HttpMethod.Get));
    }

    [Fact]
    public async Task A_redirect_is_refused_and_not_followed()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);
        api.FailPage(1, HttpStatusCode.MovedPermanently);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        api.Requests.Count.ShouldBe(1);
        api.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_protected_version_without_a_release_warns_and_the_run_continues()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--protect", "0.1.0-alpha.99.1"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Error.ShouldContain("warning: protected version 0.1.0-alpha.99.1 has no release v0.1.0-alpha.99.1.", StringComparison.Ordinal);
        result.Output.ShouldContain("delete  v0.1.0-alpha.1.1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Force_requires_a_token_and_sends_no_request_without_one()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force"], token: null);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Set GH_TOKEN or GITHUB_TOKEN", StringComparison.Ordinal);
        api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_without_a_token_sends_no_authorization()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1"], token: null);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        api.Requests.ShouldHaveSingleItem().Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task A_run_with_nothing_to_delete_reports_it()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "3", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("Deleted 0 prereleases; their tags stay.", StringComparison.Ordinal);
        api.Deleted.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--keep 0", "--keep requires a whole number from 1")]
    [InlineData("--keep -1", "--keep requires a whole number from 1")]
    [InlineData("--keep three", "--keep requires a whole number from 1")]
    [InlineData("--keep +2", "--keep requires a whole number from 1")]
    [InlineData("--keep 1 --protect v0.1.0-alpha.1.1", "--protect requires a package version")]
    [InlineData("--keep 1 --protect 01.0.0", "--protect requires a package version")]
    [InlineData("--keep 1 --protect 1.0.0+build", "--protect requires a package version")]
    [InlineData("--keep 1 --protect 1.0.0-alpha..1", "--protect requires a package version")]
    [InlineData("--keep 1 --protect", "--protect")]
    [InlineData("--protect 1.0.0", "--keep")]
    public async Task Invalid_options_exit_1_without_a_request(string options, string expectedError)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(options);
        using var api = new FakeGitHubReleasesApi();
        api.AddPrereleases(2);

        // Act
        var result = await PrereleasePruningRuns.Run(api, options.Split(' '));

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        api.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("octo-org")]
    [InlineData("octo-org/octo-lib/extra")]
    [InlineData("octo-org/..")]
    [InlineData("https://api.github.com/octo-org")]
    public async Task An_invalid_repository_exits_1_without_a_request(string repository)
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();

        // Act
        var result = await RepositoryToolInvocation.RunPruning(
            ["prune-prereleases", "--repository", repository, "--keep", "1"], PrereleasePruningRuns.Services(api, PrereleasePruningRuns.Token), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--repository requires owner/name", StringComparison.Ordinal);
        api.Requests.ShouldBeEmpty();
    }
}
