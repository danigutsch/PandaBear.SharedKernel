using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests AppHostModel, which reads an AppHost's application model (projects, waits, environment, health checks) without starting it.</summary>
public sealed class AppHostModelTests
{
    [Fact]
    public async Task Project_finds_a_project_resource_by_name()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder();
        builder.AddProject("web", RepositoryProjects.TestApp);
        builder.AddProject("api", RepositoryProjects.TestApp);

        // Act
        var project = AppHostModel.Project(builder, "api");

        // Assert
        project.Name.ShouldBe("api");
    }

    [Fact]
    public async Task A_missing_project_is_reported_by_name()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder();
        builder.AddProject("web", RepositoryProjects.TestApp);

        // Act
        Action find = () => AppHostModel.Project(builder, "missing");

        // Assert
        find.ShouldThrow<InvalidOperationException>().Message.ShouldContain("'missing'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitedFor_lists_the_waited_resources_in_declaration_order()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder();
        var database = builder.AddContainer("database", "image");
        var cache = builder.AddContainer("cache", "image");
        var web = builder.AddProject("web", RepositoryProjects.TestApp).WaitFor(database).WaitFor(cache);

        // Act
        var waited = AppHostModel.WaitedFor(web.Resource);

        // Assert
        waited.ShouldBe(["database", "cache"]);
    }

    [Fact]
    public async Task EvaluateEnvironment_keeps_references_as_model_objects()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder();
        var orders = builder.AddConnectionString("orders");
        var web = builder.AddProject("web", RepositoryProjects.TestApp).WithEnvironment("Feature", "on").WithReference(orders);

        // Act
        var environment = await AppHostModel.EvaluateEnvironment(web.Resource, TestContext.Current.CancellationToken);

        // Assert
        environment["Feature"].ShouldBe("on");
        AppHostModel.ConnectionResource(environment["ConnectionStrings__orders"]).Name.ShouldBe("orders");
    }

    [Fact]
    public void ConnectionResource_refuses_a_value_that_is_not_a_connection_string_reference()
    {
        // Arrange
        object value = "Host=localhost";

        // Act
        Action resolve = () => AppHostModel.ConnectionResource(value);

        // Assert
        resolve.ShouldThrow<InvalidOperationException>().Message.ShouldContain("String", StringComparison.Ordinal);
    }

    [Fact]
    public async Task HasHttpHealthCheck_sees_the_check_and_its_path()
    {
        // Arrange
        await using var builder = AspireTestApplication.CreateBuilder();
        var web = builder.AddProject("web", RepositoryProjects.TestApp).WithHttpEndpoint(name: "http").WithHttpHealthCheck("/health");
        var worker = builder.AddProject("worker", RepositoryProjects.TestApp).WithHttpEndpoint(name: "http");

        // Act
        var any = AppHostModel.HasHttpHealthCheck(web.Resource);
        var health = AppHostModel.HasHttpHealthCheck(web.Resource, "/health");
        var alive = AppHostModel.HasHttpHealthCheck(web.Resource, "/alive");
        var none = AppHostModel.HasHttpHealthCheck(worker.Resource);

        // Assert
        any.ShouldBeTrue();
        health.ShouldBeTrue();
        alive.ShouldBeFalse();
        none.ShouldBeFalse();
    }
}
