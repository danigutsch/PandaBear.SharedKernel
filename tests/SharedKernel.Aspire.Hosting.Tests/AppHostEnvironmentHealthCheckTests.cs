using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>
/// Tests WithHttpHealthCheckInAppHostEnvironment: the project runs in the AppHost's environment, so the health check is added exactly when the
/// project maps the health endpoint.
/// </summary>
public sealed class AppHostEnvironmentHealthCheckTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task The_project_runs_in_the_app_host_environment_and_gets_the_health_check(string environmentName)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(["--environment", environmentName]);
        var web = builder.AddContainer("web", "image").WithHttpEndpoint(name: "http", targetPort: 8080);

        // Act
        web.WithHttpHealthCheckInAppHostEnvironment("/ready");
        var environment = await EnvironmentVariables.Of(web.Resource);

        // Assert
        environment["ASPNETCORE_ENVIRONMENT"].ShouldBe(environmentName);
        web.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("web_http_/ready_200_check");
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task With_development_only_endpoints_the_check_is_added_only_in_development(string environmentName, bool expected)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(["--environment", environmentName]);
        var web = builder.AddContainer("web", "image").WithHttpEndpoint(name: "http", targetPort: 8080);

        // Act
        web.WithHttpHealthCheckInAppHostEnvironment(onlyInDevelopment: true);
        var environment = await EnvironmentVariables.Of(web.Resource);

        // Assert
        environment["ASPNETCORE_ENVIRONMENT"].ShouldBe(environmentName);
        web.Resource.Annotations.OfType<HealthCheckAnnotation>().Any().ShouldBe(expected);
    }

    [Theory]
    [InlineData("health")]
    [InlineData(" ")]
    public void A_path_that_does_not_start_with_a_slash_is_refused(string path)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var web = builder.AddContainer("web", "image").WithHttpEndpoint(name: "http", targetPort: 8080);

        // Act
        Action add = () => web.WithHttpHealthCheckInAppHostEnvironment(path);

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("path");
    }
}
