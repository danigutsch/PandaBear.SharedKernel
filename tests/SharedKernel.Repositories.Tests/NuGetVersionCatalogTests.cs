using System.Text.Json;
using PandaBear.Processes;

namespace PandaBear.Repositories.Tests;

/// <summary>Tests the request and response boundary of the isolated SDK metadata process.</summary>
public sealed class NuGetVersionCatalogTests
{
    private const string Valid = """
        {"schemaVersion":1,"packages":[{"package":"Owner","status":"complete","versions":["1.2.0","1.10.0"]}]}
        """;

    private static readonly NuGetVersionRequestPackage[] Requests = [new("Owner", ["1.0.0"], [])];
    private static readonly ProcessOutput Success = new(0, string.Empty);
    private static readonly ProcessOutput Failure = new(1, string.Empty, "diagnostics that stay in the process");

    [Theory]
    [InlineData(0, "resolverRestoreFailed")]
    [InlineData(1, "resolverBuildFailed")]
    [InlineData(2, "resolverExecutionFailed")]
    public async Task Resolver_stops_when_a_process_phase_fails(int successfulPhases, string status)
    {
        // Arrange
        var runner = new NuGetCatalogProcessResponses([.. Enumerable.Repeat(Success, successfulPhases), Failure]);

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", Requests, TestContext.Current.CancellationToken, runner);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Package.ShouldBe("Owner");
        result.Status.ShouldBe(status);
        result.Versions.ShouldBeEmpty();
        runner.Runs.ShouldHaveCount(successfulPhases + 1);
    }

    [Fact]
    public async Task Resolver_reports_a_process_timeout_without_exposing_the_response()
    {
        // Arrange
        var runner = new NuGetCatalogProcessResponses(Success, Success);

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", Requests, TestContext.Current.CancellationToken, runner);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Status.ShouldBe("resolverUnavailable");
        result.Versions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unstructured output")]
    [InlineData("null")]
    [InlineData("""{"schemaVersion":2,"packages":[]}""")]
    [InlineData("""{"schemaVersion":1,"packages":null}""")]
    [InlineData("""{"schemaVersion":1,"packages":[null]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Other","status":"complete","versions":[]}]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Owner","status":"unexpected","versions":[]}]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Owner","status":"complete","versions":null}]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Owner","status":"complete","versions":[null]}]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Owner","status":"complete","versions":["1.2.0","1.2.0"]}]}""")]
    [InlineData("""{"schemaVersion":1,"packages":[{"package":"Owner","status":"complete","versions":["[1.2.0,)"]}]}""")]
    public async Task Resolver_rejects_an_invalid_metadata_response(string response)
    {
        // Arrange
        var runner = new NuGetCatalogProcessResponses(Success, Success, new(0, response));

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", Requests, TestContext.Current.CancellationToken, runner);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Status.ShouldBe("resolverResponseInvalid");
        result.Versions.ShouldBeEmpty();
        runner.Runs.ShouldHaveCount(3);
    }

    [Fact]
    public async Task Resolver_rejects_a_response_with_more_versions_than_requested()
    {
        // Arrange
        var versions = Enumerable.Range(1, 513).Select(version => FormattableString.Invariant($"1.{version}.0")).ToArray();
        var payload = JsonSerializer.Serialize(new NuGetVersionResponse(1, [new("Owner", "complete", versions)]),
            NuGetRestoreGraphJsonContext.Default.NuGetVersionResponse);
        var runner = new NuGetCatalogProcessResponses(Success, Success, new(0, payload));

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", Requests, TestContext.Current.CancellationToken, runner);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Status.ShouldBe("resolverResponseInvalid");
        result.Versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolver_preserves_owner_order_and_uses_an_offline_restore_for_its_helper()
    {
        // Arrange
        var runner = new NuGetCatalogProcessResponses(Success, Success, new(0, Valid));

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", Requests, TestContext.Current.CancellationToken, runner);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Status.ShouldBe("complete");
        result.Versions.ShouldBe(["1.2.0", "1.10.0"]);
        runner.Runs.Select(run => run.ArgumentList[0]).Take(2).ShouldBe(["restore", "build"]);
        var restore = runner.Runs[0].ArgumentList;
        var build = runner.Runs[1].ArgumentList;
        restore.ShouldContain("--configfile");
        build.ShouldContain("--no-restore");
        var query = runner.Runs[2].ArgumentList;
        Path.GetFileName(query[0]).ShouldBe("Resolver.dll");
        Path.GetFileName(query[1]).ShouldBe("request.json");
    }
}
