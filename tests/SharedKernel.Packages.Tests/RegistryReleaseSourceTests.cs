using System.Net;
using PandaBear.Results;
using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the crates.io and npm release source against fake HTTP answers.</summary>
[Trait("Capability", "Packages")]
public sealed class RegistryReleaseSourceTests
{
    private const string Crate = """{"crate":{"max_stable_version":"15.2.0","max_version":"16.0.0-rc.1"},"versions":[{"num":"15.2.0"}]}""";
    private const string CrateVersion = """{"version":{"num":"14.1.0","created_at":"2024-01-06T19:41:25.735878Z","yanked":false}}""";
    private const string Npm = """{"name":"lodash","dist-tags":{"latest":"4.18.1"},"time":{"created":"2012-04-23T16:37:11.912Z","4.18.1":"2026-10-01T08:30:00.000Z","4.17.15":"2019-07-19T01:31:14.000Z"}}""";
    private static readonly CancellationToken Cancel = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cargo_latest_is_the_max_stable_version_from_crates_io()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(Crate));

        // Act
        var result = await RegistryFake.Source(handler).GetLatest(PackageManagerKind.Cargo, Name("ripgrep"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe("15.2.0");
        handler.Requests.ShouldHaveSingleItem().Uri.AbsoluteUri.ShouldBe("https://crates.io/api/v1/crates/ripgrep");
        handler.Requests[0].UserAgent.ShouldStartWith("sharedkernel-packages/", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cargo_release_date_comes_from_the_version_endpoint()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(CrateVersion));

        // Act
        var result = await RegistryFake.Source(handler).GetReleaseDate(PackageManagerKind.Cargo, Name("ripgrep"), Version("14.1.0"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeSuccess().ShouldBe(new DateTimeOffset(2024, 1, 6, 19, 41, 25, TimeSpan.Zero).AddTicks(7358780));
        handler.Requests.ShouldHaveSingleItem().Uri.AbsoluteUri.ShouldBe("https://crates.io/api/v1/crates/ripgrep/14.1.0");
    }

    [Fact]
    public async Task Npm_latest_and_release_date_come_from_one_cached_request()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(Npm));
        var source = RegistryFake.Source(handler);

        // Act
        var latest = await source.GetLatest(PackageManagerKind.Npm, Name("lodash"), NetworkAccess.Allowed, Cancel);
        var date = await source.GetReleaseDate(PackageManagerKind.Npm, Name("lodash"), Version("4.18.1"), NetworkAccess.Allowed, Cancel);

        // Assert
        latest.ShouldBeSuccess().Value.ShouldBe("4.18.1");
        date.ShouldBeSuccess().ShouldBe(new DateTimeOffset(2026, 10, 1, 8, 30, 0, TimeSpan.Zero));
        handler.Requests.ShouldHaveSingleItem().Uri.AbsoluteUri.ShouldBe("https://registry.npmjs.org/lodash");
    }

    [Fact]
    public async Task A_scoped_npm_name_is_escaped_into_one_path_segment()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(Npm));

        // Act
        await RegistryFake.Source(handler).GetLatest(PackageManagerKind.Npm, Name("@types/node"), NetworkAccess.Allowed, Cancel);

        // Assert
        handler.Requests.ShouldHaveSingleItem().Uri.AbsoluteUri.ShouldBe("https://registry.npmjs.org/%40types%2Fnode");
    }

    [Theory]
    [InlineData(PackageManagerKind.Apt)]
    [InlineData(PackageManagerKind.Flatpak)]
    [InlineData((PackageManagerKind)99)]
    public async Task A_manager_without_a_registry_is_an_explicit_unsupported_error(PackageManagerKind manager)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json("{}"));
        var source = RegistryFake.Source(handler);

        // Act
        var latest = await source.GetLatest(manager, Name("a"), NetworkAccess.Allowed, Cancel);
        var date = await source.GetReleaseDate(manager, Name("a"), Version("1"), NetworkAccess.Allowed, Cancel);

        // Assert
        latest.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        date.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_denied_network_is_refused_without_a_request()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(Npm));
        var source = RegistryFake.Source(handler);

        // Act
        var latest = await source.GetLatest(PackageManagerKind.Npm, Name("lodash"), NetworkAccess.Denied, Cancel);
        var date = await source.GetReleaseDate(PackageManagerKind.Npm, Name("lodash"), Version("1"), NetworkAccess.Denied, Cancel);

        // Assert
        latest.ShouldBeFailure(ResultStatus.Forbidden, "forbidden");
        date.ShouldBeFailure(ResultStatus.Forbidden, "forbidden");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PackageManagerKind.Cargo, "{\"crate\":{\"max_stable_version\":null}}", ResultStatus.NotFound)]
    [InlineData(PackageManagerKind.Cargo, "{}", ResultStatus.NotFound)]
    [InlineData(PackageManagerKind.Npm, "{\"dist-tags\":{}}", ResultStatus.NotFound)]
    [InlineData(PackageManagerKind.Npm, "{}", ResultStatus.NotFound)]
    [InlineData(PackageManagerKind.Cargo, "not json", ResultStatus.Unavailable)]
    [InlineData(PackageManagerKind.Npm, "not json", ResultStatus.Unavailable)]
    [InlineData(PackageManagerKind.Npm, "null", ResultStatus.Unavailable)]
    public async Task An_answer_without_a_latest_version_is_a_failure(PackageManagerKind manager, string body, ResultStatus expected)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(_ => FakeHttpHandler.Json(body));

        // Act
        var result = await RegistryFake.Source(handler).GetLatest(manager, Name("a"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(PackageManagerKind.Cargo, "{\"version\":{\"created_at\":\"yesterday\"}}")]
    [InlineData(PackageManagerKind.Cargo, "{\"version\":{}}")]
    [InlineData(PackageManagerKind.Cargo, "{}")]
    [InlineData(PackageManagerKind.Npm, "{\"time\":{\"1.0.0\":\"yesterday\"}}")]
    [InlineData(PackageManagerKind.Npm, "{\"time\":{}}")]
    [InlineData(PackageManagerKind.Npm, "{}")]
    public async Task A_missing_or_unreadable_release_time_is_a_failure(PackageManagerKind manager, string body)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(_ => FakeHttpHandler.Json(body));

        // Act
        var result = await RegistryFake.Source(handler).GetReleaseDate(manager, Name("a"), Version("1.0.0"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ResultStatus.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, ResultStatus.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, ResultStatus.Unavailable)]
    [InlineData(HttpStatusCode.Redirect, ResultStatus.Unavailable)]
    public async Task An_http_error_is_a_failure_with_a_matching_status(HttpStatusCode code, ResultStatus expected)
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(_ => FakeHttpHandler.Json("secret-body", code));

        // Act
        var result = await RegistryFake.Source(handler).GetLatest(PackageManagerKind.Cargo, Name("a"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.Status.ShouldBe(expected);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("secret-body", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_response_that_came_from_another_host_is_refused()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static request =>
        {
            var response = FakeHttpHandler.Json(Npm);
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/lodash");
            return response;
        });

        // Act
        var result = await RegistryFake.Source(handler).GetLatest(PackageManagerKind.Npm, Name("lodash"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("another host", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_oversized_response_is_refused()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UnsizedStream(new byte[RegistryReleaseSource.MaxResponseBytes + 1])) });

        // Act
        var result = await RegistryFake.Source(handler).GetLatest(PackageManagerKind.Npm, Name("lodash"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("larger than", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_registry_that_does_not_answer_in_time_is_a_failure()
    {
        // Arrange
        using var handler = new FakeHttpHandler(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage();
        });

        // Act
        var result = await RegistryFake.Source(handler, timeout: TimeSpan.FromMilliseconds(50)).GetLatest(PackageManagerKind.Npm, Name("lodash"), NetworkAccess.Allowed, Cancel);

        // Assert
        result.ShouldBeFailure(ResultStatus.Unavailable, "unavailable").Detail.ShouldContain("no answer within", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answers_are_cached_until_they_expire_and_failures_are_not_cached()
    {
        // Arrange
        var time = new TestTimeProvider(OsvFake.Start);
        var fail = true;
        using var handler = FakeHttpHandler.Answering(_ => fail ? FakeHttpHandler.Json("x", HttpStatusCode.BadGateway) : FakeHttpHandler.Json(Crate));
        var source = RegistryFake.Source(handler, time, TimeSpan.FromMinutes(10));
        var failed = await source.GetLatest(PackageManagerKind.Cargo, Name("a"), NetworkAccess.Allowed, Cancel);
        fail = false;

        // Act
        var first = await source.GetLatest(PackageManagerKind.Cargo, Name("a"), NetworkAccess.Allowed, Cancel);
        var cached = await source.GetLatest(PackageManagerKind.Cargo, Name("a"), NetworkAccess.Allowed, Cancel);
        var countBeforeExpiry = handler.Requests.Count;
        time.Advance(TimeSpan.FromMinutes(10));
        await source.GetLatest(PackageManagerKind.Cargo, Name("a"), NetworkAccess.Allowed, Cancel);

        // Assert
        failed.IsFailure.ShouldBeTrue();
        first.ShouldBeSuccess().Value.ShouldBe("15.2.0");
        cached.ShouldBeSuccess().Value.ShouldBe("15.2.0");
        countBeforeExpiry.ShouldBe(2);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_release_date_is_cached_per_version()
    {
        // Arrange
        using var handler = FakeHttpHandler.Answering(static _ => FakeHttpHandler.Json(CrateVersion));
        var source = RegistryFake.Source(handler);

        // Act
        await source.GetReleaseDate(PackageManagerKind.Cargo, Name("a"), Version("1.0.0"), NetworkAccess.Allowed, Cancel);
        await source.GetReleaseDate(PackageManagerKind.Cargo, Name("a"), Version("1.0.0"), NetworkAccess.Allowed, Cancel);
        await source.GetReleaseDate(PackageManagerKind.Cargo, Name("a"), Version("1.0.1"), NetworkAccess.Allowed, Cancel);

        // Assert
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public void The_constructor_rejects_bad_arguments()
    {
        // Arrange
        using var client = new HttpClient();

        // Act
        Func<object?> noClient = () => new RegistryReleaseSource(null!, TimeProvider.System);
        Func<object?> noTime = () => new RegistryReleaseSource(client, null!);
        Func<object?> badTimeout = () => new RegistryReleaseSource(client, TimeProvider.System, requestTimeout: TimeSpan.FromSeconds(-1));

        // Assert
        noClient.ShouldThrow<ArgumentNullException>();
        noTime.ShouldThrow<ArgumentNullException>();
        badTimeout.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void The_default_handler_follows_no_redirect_and_the_user_agent_is_generic()
    {
        // Act
        using var handler = PackageHttp.CreateHandler();

        // Assert
        handler.ShouldBeOfType<SocketsHttpHandler>().AllowAutoRedirect.ShouldBeFalse();
        PackageHttp.UserAgent.ShouldNotContain("@", StringComparison.Ordinal);
    }
}
