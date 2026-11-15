using Aspire.Hosting;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests WithSelfOrigin: a resource receives the URL of one of its own endpoints, never a hard-coded address.</summary>
public sealed class SelfOriginTests
{
    [Fact]
    public async Task Without_an_endpoint_name_the_https_endpoint_is_used()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var site = builder.AddExecutable("site", "dotnet", ".").WithHttpEndpoint(name: "http", targetPort: 8080).WithHttpsEndpoint(name: "https", targetPort: 8443);
        AllocatedEndpoints.Allocate(site.Resource, "http", 5000);
        AllocatedEndpoints.Allocate(site.Resource, "https", 5001);

        // Act
        site.WithSelfOrigin("PublicSite__Origin");
        var environment = await EnvironmentVariables.Of(site.Resource);

        // Assert
        environment["PublicSite__Origin"].ShouldBe("https://localhost:5001");
    }

    [Fact]
    public async Task Without_an_endpoint_name_and_without_https_the_HTTP_endpoint_is_used()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var site = builder.AddExecutable("site", "dotnet", ".").WithHttpEndpoint(name: "http", targetPort: 8080);
        AllocatedEndpoints.Allocate(site.Resource, "http", 5000);

        // Act
        site.WithSelfOrigin("PublicSite__Origin");
        var environment = await EnvironmentVariables.Of(site.Resource);

        // Assert
        environment["PublicSite__Origin"].ShouldBe("http://localhost:5000");
    }

    [Fact]
    public async Task A_named_endpoint_is_used_even_when_https_exists()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var site = builder.AddExecutable("site", "dotnet", ".").WithHttpEndpoint(name: "public", targetPort: 8080).WithHttpsEndpoint(name: "https", targetPort: 8443);
        AllocatedEndpoints.Allocate(site.Resource, "public", 5000);
        AllocatedEndpoints.Allocate(site.Resource, "https", 5001);

        // Act
        site.WithSelfOrigin("PublicSite__Origin", "public");
        var environment = await EnvironmentVariables.Of(site.Resource);

        // Assert
        environment["PublicSite__Origin"].ShouldBe("http://localhost:5000");
    }

    [Fact]
    public async Task A_named_endpoint_that_does_not_exist_fails_with_its_name_instead_of_falling_back()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var site = builder.AddExecutable("site", "dotnet", ".").WithHttpEndpoint(name: "http", targetPort: 8080);
        AllocatedEndpoints.Allocate(site.Resource, "http", 5000);
        site.WithSelfOrigin("PublicSite__Origin", "public");

        // Act
        Func<Task> evaluate = () => EnvironmentVariables.Of(site.Resource);

        // Assert
        var failure = await evaluate.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldContain("'public'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_resource_without_HTTP_or_https_endpoints_fails()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var site = builder.AddExecutable("site", "dotnet", ".");
        site.WithSelfOrigin("PublicSite__Origin");

        // Act
        Func<Task> evaluate = () => EnvironmentVariables.Of(site.Resource);

        // Assert
        var failure = await evaluate.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldContain("'site'", StringComparison.Ordinal);
    }
}
