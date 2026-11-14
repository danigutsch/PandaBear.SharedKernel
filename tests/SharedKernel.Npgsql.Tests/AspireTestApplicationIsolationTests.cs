using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Tests that every builder AspireTestApplication creates is isolated (its own DCP resource-name suffix), takes configuration values, and
/// starts projects that do not watch their configuration files.
/// </summary>
public sealed class AspireTestApplicationIsolationTests
{
    private const string SuffixKey = "DcpPublisher:ResourceNameSuffix";

    [Fact]
    public async Task Every_builder_gets_its_own_dcp_resource_name_suffix()
    {
        // Arrange
        await using var first = AspireTestApplication.CreateBuilder();
        await using var second = AspireTestApplication.CreateBuilder();

        // Act
        var suffixes = new[] { first.Configuration[SuffixKey], second.Configuration[SuffixKey] };

        // Assert
        suffixes.ShouldAllSatisfy(suffix => string.IsNullOrWhiteSpace(suffix).ShouldBeFalse(), "a builder without a suffix shares DCP resource names with parallel tests");
        suffixes.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public async Task A_suffix_the_caller_names_is_kept()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder($"--{SuffixKey}=stack-1");

        // Act
        var suffix = builder.Configuration[SuffixKey];

        // Assert
        suffix.ShouldBe("stack-1");
    }

    [Fact]
    public async Task Configuration_values_reach_the_builder_and_arguments_win_over_them()
    {
        // Arrange
        var configuration = new Dictionary<string, string?> { ["Only:Configured"] = "configured", ["Both"] = "configured" };

        // Act
        await using var builder = AspireTestApplication.CreateBuilder(configuration, "--Both=argument");

        // Assert
        builder.Configuration["Only:Configured"].ShouldBe("configured");
        builder.Configuration["Both"].ShouldBe("argument");
        string.IsNullOrWhiteSpace(builder.Configuration[SuffixKey]).ShouldBeFalse("configuration must not bypass the suffix");
    }

    [Fact]
    public async Task Configuration_values_win_over_environment_variables()
    {
        // Arrange
        const string key = "SkIsolationTest:DataFolder";
        var configuration = new Dictionary<string, string?> { [key] = "/tmp/test-run" };
        using var environment = new EnvironmentVariableScope("SkIsolationTest__DataFolder", "/home/someone/real-data");

        // Act
        await using var builder = AspireTestApplication.CreateBuilder(configuration);

        // Assert
        builder.Configuration[key].ShouldBe("/tmp/test-run", "a developer's environment must not point a test run at real data");
    }

    [Fact]
    public async Task Started_projects_do_not_watch_their_configuration_files()
    {
        // Arrange
        var builder = AspireTestApplication.CreateBuilder();
        var web = builder.AddProject("web", RepositoryProjects.TestApp).WithExplicitStart();

        // Act
        await using var app = await AspireTestApplication.Start(builder, [], null, TestContext.Current.CancellationToken);
        var environment = await AppHostModel.EvaluateEnvironment(web.Resource, TestContext.Current.CancellationToken);

        // Assert
        environment["DOTNET_hostBuilder__reloadConfigOnChange"].ShouldBe("false", "one file watcher per configuration file and host exhausts the inotify limit in parallel runs");
    }

    [Fact]
    public void An_app_host_launch_without_configuration_has_none()
    {
        // Arrange
        var launch = new AppHostLaunch(["--A=1"], ["web"], TimeSpan.FromMinutes(1));

        // Act
        var configuration = launch.Configuration;

        // Assert
        configuration.ShouldBeEmpty();
    }
}
