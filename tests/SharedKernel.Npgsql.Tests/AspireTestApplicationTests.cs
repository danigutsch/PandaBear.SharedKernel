using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

public sealed class AspireTestApplicationTests
{
    [Fact]
    public async Task Container_endpoints_use_direct_dynamic_ports_without_runtime_port_allocation()
    {
        // Arrange
        var builder = AspireTestApplication.CreateBuilder();
        var postgres = builder.AddPostgres("port-isolation-postgres");

        // Act
        await using var app = await AspireTestApplication.Start(
            builder, [postgres.Resource.Name], null, TestContext.Current.CancellationToken);
        var endpoint = postgres.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        var connectionString = await app.GetConnectionString(postgres.Resource.Name, TestContext.Current.CancellationToken);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Assert
        endpoint.IsProxied.ShouldBeFalse();
        connection.Port.ShouldBeInRange(10000, 32767);
        connection.State.ShouldBe(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task Teardown_waits_for_cleanup_longer_than_the_generic_host_default()
    {
        // Arrange
        var builder = AspireTestApplication.CreateBuilder();
        var cleanup = new SlowCleanupHostedService();
        builder.Services.AddSingleton<IHostedService>(cleanup);
        var app = await AspireTestApplication.Start(builder, [], null, TestContext.Current.CancellationToken);

        // Act
        await app.DisposeAsync();

        // Assert
        cleanup.Completed.ShouldBeTrue();
    }
}
