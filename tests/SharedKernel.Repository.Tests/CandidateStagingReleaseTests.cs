using System.Net;
using System.Security.Cryptography;
using System.Text;
using PandaBear.CI.Tool;
using PandaBear.Testing;

namespace PandaBear.Repository.Tests;

/// <summary>Tests staging pinned candidate packages by downloading them from a published release, using a fake release server.</summary>
public sealed class CandidateStagingReleaseTests
{
    [Fact]
    public async Task Release_staging_matches_the_release_manifest_then_downloads_only_missing_packages()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var cached = scope.PackageNames[0];
        var missing = scope.PackageNames[1];
        Directory.CreateDirectory(scope.Output);
        await File.WriteAllBytesAsync(Path.Combine(scope.Output, cached), scope.Packages[cached], TestContext.Current.CancellationToken);

        // Evidence fields of the published manifest legitimately differ from the pin.
        var release = scope.CreatePin();
        release["windowsPackageConsumption"] = "passed";
        release["linuxPackageConsumption"] = "failed";
        using var server = FakeReleaseServer.For(scope, Encoding.UTF8.GetBytes(release.ToJsonString()));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        server.Addresses.ShouldBe(
            [FakeReleaseServer.ReleaseUrl("candidate.json"), FakeReleaseServer.ReleaseUrl(missing), FakeReleaseServer.AssetUrl(missing)],
            "The manifest is always fetched, and only the missing package is downloaded.");
        server.Requests.ShouldAllSatisfy(static request =>
        {
            request.UserAgent.ShouldStartWith("sharedkernel-ci/", StringComparison.Ordinal);
            request.HasAuthorization.ShouldBeFalse("Release downloads never send credentials.");
        });
        var staged = await File.ReadAllBytesAsync(Path.Combine(scope.Output, missing), TestContext.Current.CancellationToken);
        staged.ShouldBe(scope.Packages[missing]);
        var manifest = await File.ReadAllBytesAsync(Path.Combine(scope.Output, "candidate.json"), TestContext.Current.CancellationToken);
        manifest.ShouldBe(scope.Pin, "candidate.json is the pin, not the published manifest.");
        result.Output.ShouldContain("1 package added, 1 already present.", StringComparison.Ordinal);
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("revision", "does not match the pin: libraryRevision differs")]
    [InlineData("hash", "does not match the pin: packageSha256 differs")]
    [InlineData("extra-package", "does not match the pin: packageSha256 differs")]
    [InlineData("dirty", "libraryDirty must be false")]
    public async Task Release_manifest_must_have_the_identity_of_the_pin(string difference, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var release = scope.CreatePin();
        var hashes = release["packageSha256"]!.AsObject();
        switch (difference)
        {
            case "revision":
                release["libraryRevision"] = new string('e', 40);
                break;
            case "hash":
                hashes[scope.PackageNames[0]] = new string('0', 64);
                break;
            case "extra-package":
                hashes["PandaBear.SharedKernel.Extra." + CandidateStagingScope.Version + ".nupkg"] = new string('0', 64);
                break;
            default:
                release["libraryDirty"] = true;
                break;
        }

        using var server = FakeReleaseServer.For(scope, Encoding.UTF8.GetBytes(release.ToJsonString()));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        server.Addresses.ShouldBe([FakeReleaseServer.ReleaseUrl("candidate.json")], "No package is downloaded for a mismatched release.");
        Directory.Exists(scope.Output).ShouldBeFalse();
    }

    [Fact]
    public async Task Release_staging_refuses_cached_packages_of_a_missing_release_without_retrying()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        Directory.CreateDirectory(scope.Output);
        foreach (var (name, content) in scope.Packages)
        {
            await File.WriteAllBytesAsync(Path.Combine(scope.Output, name), content, TestContext.Current.CancellationToken);
        }

        using var server = new FakeReleaseServer();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(FakeReleaseServer.ReleaseUrl("candidate.json") + " returned 404 NotFound", StringComparison.Ordinal);
        server.Requests.Count.ShouldBe(1, "A missing release is not retried.");
        File.Exists(Path.Combine(scope.Output, "candidate.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task Transient_failures_are_retried_after_the_requested_delay_up_to_the_cap()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = FakeReleaseServer.For(scope);
        server.Set(
            FakeReleaseServer.ReleaseUrl("candidate.json"),
            () => FakeReleaseServer.RetryAfter(HttpStatusCode.ServiceUnavailable, TimeSpan.FromSeconds(7)),
            () => FakeReleaseServer.RetryAfter(HttpStatusCode.TooManyRequests, TimeSpan.FromHours(1)),
            () => FakeReleaseServer.Ok(scope.Pin));
        var asset = FakeReleaseServer.AssetUrl(scope.PackageNames[0]);
        server.Set(asset, () => FakeReleaseServer.Status(HttpStatusCode.RequestTimeout), () => FakeReleaseServer.Ok(scope.Packages[scope.PackageNames[0]]));
        var timeProvider = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server, timeProvider),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        server.Addresses.Count(address => address == FakeReleaseServer.ReleaseUrl("candidate.json")).ShouldBe(3);
        server.Addresses.Count(address => address == asset).ShouldBe(2);
        var waits = timeProvider.Timers.Where(static timer => timer < CandidateDownloadLimits.Default.RequestTimeout).ToArray();
        waits.ShouldBe(
            [TimeSpan.FromSeconds(7), CandidateDownloadLimits.Default.MaxRetryDelay, TimeSpan.FromSeconds(1)],
            "Retry-After is honored up to the cap; without it the first retry waits one second.");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Retries_stop_after_the_attempt_limit(HttpStatusCode status)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = new FakeReleaseServer();
        server.Add(FakeReleaseServer.ReleaseUrl("candidate.json"), () => FakeReleaseServer.Status(status));
        var timeProvider = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server, timeProvider),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        server.Requests.Count.ShouldBe(CandidateDownloadLimits.Default.MaxAttempts);
        result.Error.ShouldContain($"returned {(int)status} {status} after {CandidateDownloadLimits.Default.MaxAttempts} attempts", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://release-assets.githubusercontent.com/asset", "redirected to an address that is not HTTPS")]
    [InlineData("loop", "redirected more than 5 times")]
    public async Task Redirects_must_stay_on_https_and_end(string target, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = FakeReleaseServer.For(scope);
        var package = FakeReleaseServer.ReleaseUrl(scope.PackageNames[0]);
        server.Set(package, () => FakeReleaseServer.Redirect(target == "loop" ? package : target));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        server.Addresses.ShouldNotContain(static address => address.StartsWith("http:", StringComparison.Ordinal), "A plain HTTP address is never requested.");
        Directory.Exists(scope.Output).ShouldBeFalse();
    }

    [Theory]
    [InlineData("package", "exceeds the per-package limit of 6000 bytes")]
    [InlineData("total", "exceeds the total download limit of 5000 bytes")]
    public async Task Size_limits_stop_a_download_while_it_streams(string limit, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = FakeReleaseServer.For(scope);
        var oversized = RandomNumberGenerator.GetBytes(7000);
        var limits = CandidateStagingTestServices.SmallLimits;
        if (limit == "package")
        {
            server.Set(
                FakeReleaseServer.AssetUrl(scope.PackageNames[1]),
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UnsizedStream(oversized)) });
        }
        else
        {
            limits = limits with
            {
                MaxTotalBytes = 5000
            };
        }

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server, limits: limits),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(FakeReleaseServer.ReleaseUrl(scope.PackageNames[1]) + " " + expectedError, StringComparison.Ordinal);
        Directory.Exists(scope.Output).ShouldBeFalse();
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Fact]
    public async Task Downloaded_packages_must_match_the_pinned_hash()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = FakeReleaseServer.For(scope);
        server.Set(FakeReleaseServer.AssetUrl(scope.PackageNames[0]), () => FakeReleaseServer.Ok(RandomNumberGenerator.GetBytes(4096)));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(scope.PackageNames[0] + " does not match its pinned SHA-256 hash", StringComparison.Ordinal);
        Directory.Exists(scope.Output).ShouldBeFalse();
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_request_that_outlasts_its_timeout_fails()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = new FakeReleaseServer();
        server.Add(FakeReleaseServer.ReleaseUrl("candidate.json"), static async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return FakeReleaseServer.Status(HttpStatusCode.OK);
        });

        // Every timer fires at once, so the request timeout elapses while the server waits.
        var timeProvider = new ImmediateTimeProvider(TimeSpan.MaxValue);

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server, timeProvider),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(FakeReleaseServer.ReleaseUrl("candidate.json") + " did not finish within 300 seconds", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_concurrent_stager_may_win_the_move_with_the_identical_package_only(bool identical)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var server = FakeReleaseServer.For(scope);
        var name = scope.PackageNames[1];
        var target = Path.Combine(scope.Output, name);
        var concurrent = identical ? scope.Packages[name] : RandomNumberGenerator.GetBytes(4096);

        // Another stager moves its copy into place while this one downloads.
        server.Set(FakeReleaseServer.AssetUrl(name), () =>
        {
            Directory.CreateDirectory(scope.Output);
            File.WriteAllBytes(target, concurrent);
            return FakeReleaseServer.Ok(scope.Packages[name]);
        });

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base,
            scope.Arguments(CandidateStagingScope.Repository),
            new ProcessLog().Run,
            CandidateStagingTestServices.Create(new CandidateGit(), server),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(identical ? 0 : 2, result.Error);
        var content = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        content.ShouldBe(concurrent, "A package in the output is never overwritten.");
        File.Exists(Path.Combine(scope.Output, "candidate.json")).ShouldBe(identical);
        scope.ScratchDirectories().ShouldBeEmpty();
    }
}
