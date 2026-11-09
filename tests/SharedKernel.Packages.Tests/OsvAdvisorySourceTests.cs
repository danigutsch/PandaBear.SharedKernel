using System.Net;
using PandaBear.Results;
using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the OSV adapter against fake HTTP answers; nothing reaches the network.</summary>
[Trait("Capability", "Packages")]
public sealed class OsvAdvisorySourceTests
{
    private static readonly CancellationToken Cancel = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_range_only_record_is_exact_for_the_version_that_was_asked_about()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-aaaa-bbbb-cccc"], new()
        {
            ["GHSA-aaaa-bbbb-cccc"] = OsvAnswers.RangeOnly("GHSA-aaaa-bbbb-cccc", "lodash")
        });
        var source = OsvFake.Source(handler);

        // Act
        var result = await source.Query(PackageManagerKind.Npm, [Installed("lodash", "4.17.15")], NetworkAccess.Allowed, Cancel);

        // Assert
        var advisory = result.ShouldBeSuccess().ShouldHaveSingleItem();
        advisory.Id.ShouldBe("GHSA-aaaa-bbbb-cccc");
        advisory.Package.ShouldBe(Name("lodash"));
        advisory.Severity.ShouldBe(AdvisorySeverity.High);
        advisory.AffectedVersions.ShouldBe([Version("4.17.15")]);
        OsvFake.Queries(handler.Requests[0].Body).ShouldBe([("lodash", "npm", "4.17.15", null)]);
    }

    [Fact]
    public async Task Versions_that_a_record_lists_are_added_to_the_versions_that_were_asked_about()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1"], new()
        {
            ["GHSA-1"] = OsvAnswers.WithVersions("GHSA-1", "lodash", ["4.17.0", "4.17.15"])
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("lodash", "4.17.15")], NetworkAccess.Allowed, Cancel);

        // Assert
        var advisory = result.ShouldBeSuccess().ShouldHaveSingleItem();
        advisory.AffectedVersions.Select(static v => v.Value).ShouldBe(["4.17.0", "4.17.15"]);
        advisory.Severity.ShouldBe(AdvisorySeverity.Low);
    }

    [Fact]
    public async Task A_version_that_osv_does_not_report_as_affected_is_not_listed()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, version) => version == "4.17.15" ? ["GHSA-1"] : null, new()
        {
            ["GHSA-1"] = OsvAnswers.RangeOnly("GHSA-1", "lodash")
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("lodash", "4.17.15"), Installed("lodash", "4.18.1")], NetworkAccess.Allowed, Cancel);

        // Assert
        var advisory = result.ShouldBeSuccess().ShouldHaveSingleItem();
        advisory.Affects(Version("4.17.15")).ShouldBeTrue();
        advisory.Affects(Version("4.18.1")).ShouldBeFalse();
        OsvFake.Queries(handler.Requests[0].Body).Length.ShouldBe(2);
    }

    [Fact]
    public async Task No_vulnerability_is_a_successful_empty_answer()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Cargo, [Installed("bat", "0.26.1")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_withdrawn_record_is_left_out()
    {
        // Arrange
        var withdrawn = """{"id":"GHSA-1","withdrawn":"2026-01-01T00:00:00Z","affected":[{"package":{"name":"lodash","ecosystem":"npm"}}]}""";
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1"], new()
        {
            ["GHSA-1"] = withdrawn
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("lodash", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_record_without_a_readable_severity_gets_the_explicit_default_of_high()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1"], new()
        {
            ["GHSA-1"] = OsvAnswers.RangeOnly("GHSA-1", "lodash", databaseSeverity: null)
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("lodash", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldHaveSingleItem().Severity.ShouldBe(AdvisorySeverity.High);
    }

    [Fact]
    public async Task A_vulnerability_that_two_packages_share_is_read_once()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1"], new()
        {
            ["GHSA-1"] = OsvAnswers.RangeOnly("GHSA-1", "a")
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0"), Installed("b", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().Select(static a => a.Package.Value).ShouldBe(["a", "b"]);
        handler.Requests.Count(static r => r.Uri.AbsolutePath.StartsWith("/v1/vulns/", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task A_denied_network_is_refused_without_any_request()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1")], NetworkAccess.Denied, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Forbidden, "forbidden");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PackageManagerKind.Flatpak)]
    [InlineData((PackageManagerKind)99)]
    public async Task A_manager_without_an_osv_ecosystem_is_an_error_not_an_empty_answer(PackageManagerKind manager)
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);

        // Act
        var result = await OsvFake.Source(handler).Query(manager, [Installed("a", "1")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Apt_queries_the_given_release_and_fails_without_one()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);

        // Act
        var without = await OsvFake.Source(handler).Query(PackageManagerKind.Apt, [Installed("openssl", "3.0")], NetworkAccess.Allowed, Cancel);
        var with = await OsvFake.Source(handler, apt: "Debian:12").Query(PackageManagerKind.Apt, [Installed("openssl", "3.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        without.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        with.ShouldBeSuccess().ShouldBeEmpty();
        OsvFake.Queries(handler.Requests.ShouldHaveSingleItem().Body).ShouldHaveSingleItem().Ecosystem.ShouldBe("Debian:12");
    }

    [Fact]
    public async Task An_empty_package_list_makes_no_request()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Packages_are_sent_in_batches_of_one_hundred()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);
        var packages = Enumerable.Range(0, 250).Select(static i => Installed($"pkg{i:000}", "1.0.0")).ToArray();

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, packages, NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
        handler.Requests.Select(static r => OsvFake.Queries(r.Body).Length).ShouldBe([100, 100, 50]);
    }

    [Fact]
    public async Task A_failing_later_batch_fails_the_whole_lookup_even_when_the_first_found_advisories()
    {
        // Arrange
        var batches = 0;
        using var handler = FakeHttpHandler.Answering(request =>
        {
            if (request.Uri.AbsolutePath == "/v1/querybatch")
            {
                var queries = OsvFake.Queries(request.Body);
                return ++batches == 1
                    ? FakeHttpHandler.Json(OsvAnswers.Batch([.. queries.Select(static _ => (string[]?)["GHSA-1"])]))
                    : FakeHttpHandler.Json("{}", HttpStatusCode.InternalServerError);
            }

            return FakeHttpHandler.Json(OsvAnswers.RangeOnly("GHSA-1", "pkg000"));
        });
        var packages = Enumerable.Range(0, 101).Select(static i => Installed($"pkg{i:000}", "1.0.0")).ToArray();

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, packages, NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable");
    }

    [Fact]
    public async Task A_result_with_a_next_page_token_is_followed_with_only_that_query()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(request =>
        {
            if (request.Uri.AbsolutePath != "/v1/querybatch")
            {
                var id = Uri.UnescapeDataString(request.Uri.AbsolutePath["/v1/vulns/".Length..]);
                return FakeHttpHandler.Json(OsvAnswers.RangeOnly(id, "a"));
            }

            var queries = OsvFake.Queries(request.Body);
            return queries[0].PageToken is null
                ? FakeHttpHandler.Json(OsvAnswers.BatchPaged([(["GHSA-1"], "next"), (["GHSA-9"], null)]))
                : FakeHttpHandler.Json(OsvAnswers.BatchPaged([(["GHSA-2"], null)]));
        });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0"), Installed("b", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().Select(static x => $"{x.Id}/{x.Package}").ShouldBe(["GHSA-1/a", "GHSA-2/a", "GHSA-9/b"]);
        var second = OsvFake.Queries(handler.Requests[1].Body);
        second.ShouldBe([("a", "npm", "1.0.0", "next")]);
    }

    [Fact]
    public async Task Endless_paging_fails_instead_of_returning_a_cut_short_answer()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(OsvAnswers.BatchPaged([(["GHSA-1"], "again")])));

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable");
        handler.Requests.Count.ShouldBe(OsvAdvisorySource.MaxPages);
    }

    [Fact]
    public async Task More_vulnerabilities_than_the_limit_fail_the_lookup()
    {
        // Arrange
        var ids = Enumerable.Range(0, OsvAdvisorySource.MaxVulnerabilities + 1).Select(static i => $"GHSA-{i}").ToArray();
        using var handler = OsvFake.Handler((_, _) => ids, []);

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable");
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("batch-500")]
    [InlineData("batch-429")]
    [InlineData("batch-not-json")]
    [InlineData("batch-no-results")]
    [InlineData("batch-too-few-results")]
    [InlineData("batch-null-result")]
    [InlineData("batch-id-missing")]
    [InlineData("batch-id-unsafe")]
    [InlineData("batch-redirect")]
    [InlineData("vuln-404")]
    [InlineData("vuln-500")]
    [InlineData("vuln-not-json")]
    [InlineData("vuln-other-id")]
    [InlineData("network-error")]
    public async Task A_failed_or_partial_lookup_is_a_failure_never_an_empty_success(string scenario)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(request => OsvFailures.Respond(scenario, request));

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("secret-body", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_response_that_declares_more_than_the_limit_is_refused()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json("{\"results\":[{}]}" + new string(' ', OsvAdvisorySource.MaxResponseBytes)));

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("larger than", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_response_without_a_length_that_grows_past_the_limit_is_refused()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UnsizedStream(new byte[OsvAdvisorySource.MaxResponseBytes + 1])) });

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("larger than", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("gzip")]
    public async Task A_body_that_fails_while_it_is_read_is_a_failure_not_an_exception(string kind)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(_ => kind == "reset" ? FaultyBodies.ConnectionReset() : FaultyBodies.CorruptGzip());

        // Act
        var result = await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        var error = result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable");
        error.Detail.ShouldContain("could not be read completely", StringComparison.Ordinal);
        error.Detail.ShouldNotContain("secret-body", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_service_that_does_not_answer_in_time_is_a_failure()
    {
        // Arrange
        using var handler = new FakeHttpHandler(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage();
        });
        var source = OsvFake.Source(handler, timeout: TimeSpan.FromMilliseconds(50));

        // Act
        var result = await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("no answer within", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_the_call_throws_instead_of_returning_a_failure()
    {
        // Arrange
        using var handler = new FakeHttpHandler(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage();
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> call = () => OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, cancellation.Token);

        // Assert
        await call.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Requests_use_https_the_osv_host_and_a_generic_user_agent()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1:x"], new()
        {
            ["GHSA-1:x"] = OsvAnswers.RangeOnly("GHSA-1:x", "a")
        });

        // Act
        await OsvFake.Source(handler).Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        handler.Requests.ShouldAllSatisfy(static r =>
        {
            r.Uri.Scheme.ShouldBe("https");
            r.Uri.Host.ShouldBe("api.osv.dev");
            r.UserAgent.ShouldStartWith("sharedkernel-packages/", StringComparison.Ordinal);
            r.UserAgent.ShouldNotContain("@", StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_second_identical_query_is_answered_from_the_cache()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => ["GHSA-1"], new()
        {
            ["GHSA-1"] = OsvAnswers.RangeOnly("GHSA-1", "a")
        });
        var source = OsvFake.Source(handler);
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);
        var before = handler.Requests.Count;

        // Act
        var second = await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        second.ShouldBeSuccess().ShouldHaveSingleItem().Affects(Version("1.0.0")).ShouldBeTrue();
        handler.Requests.Count.ShouldBe(before);
    }

    [Fact]
    public async Task A_different_version_is_a_cache_miss()
    {
        // Arrange
        using var handler = OsvFake.Handler((_, _) => null, []);
        var source = OsvFake.Source(handler);
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Act
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.1")], NetworkAccess.Allowed, Cancel);

        // Assert
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_cached_answer_expires_after_its_lifetime()
    {
        // Arrange
        var time = new TestTimeProvider(OsvFake.Start);
        using var handler = OsvFake.Handler((_, _) => null, []);
        var source = OsvFake.Source(handler, time, lifetime: TimeSpan.FromMinutes(10));
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);
        time.Advance(TimeSpan.FromMinutes(9));
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);
        var beforeExpiry = handler.Requests.Count;
        time.Advance(TimeSpan.FromMinutes(1));

        // Act
        await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        beforeExpiry.ShouldBe(1);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_failure_is_not_cached_so_the_next_call_asks_again()
    {
        // Arrange
        var fail = true;
        using var handler = FakeHttpHandler.Answering(_ =>
            fail ? FakeHttpHandler.Json("{}", HttpStatusCode.ServiceUnavailable) : FakeHttpHandler.Json(OsvAnswers.Batch([null])));
        var source = OsvFake.Source(handler);
        var first = await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);
        fail = false;

        // Act
        var second = await source.Query(PackageManagerKind.Npm, [Installed("a", "1.0.0")], NetworkAccess.Allowed, Cancel);

        // Assert
        first.IsFailure.ShouldBeTrue();
        second.ShouldBeSuccess().ShouldBeEmpty();
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public void The_constructor_rejects_a_non_positive_timeout_and_null_arguments()
    {
        // Arrange
        using var client = new HttpClient();

        // Act
        Func<object?> zeroTimeout = () => new OsvAdvisorySource(client, TimeProvider.System, requestTimeout: TimeSpan.Zero);
        Func<object?> noClient = () => new OsvAdvisorySource(null!, TimeProvider.System);

        // Assert
        zeroTimeout.ShouldThrow<ArgumentOutOfRangeException>();
        noClient.ShouldThrow<ArgumentNullException>();
    }
}
