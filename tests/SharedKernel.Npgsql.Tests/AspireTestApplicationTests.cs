using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests how AspireTestApplication configures container endpoints and application teardown.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture whose server the tests connect to.</param>
public sealed class AspireTestApplicationTests(PostgreSqlTestServerFixture fixture)
{
    // Aspire allocates proxyless ports outside the OS ephemeral range, and mixing a DCP-reserved proxy target port with a
    // container port that the runtime assigns lets Podman claim the same port. AspireTestApplication.Start prevents that by
    // turning the proxy off for every container, and this test pins the result in two halves that together cost no container:
    // - The model half starts an application whose PostgreSQL resource is marked WithExplicitStart. DCP allocates the
    //   endpoint when the application starts, whether or not the resource ever does, so the endpoint annotation already shows
    //   that the proxy is off and that the port is a direct one in DCP's range. A plain AddPostgres without WithExplicitStart
    //   would start a container here, which is what this half avoids.
    // - The real half opens a connection to the PostgreSQL server of the assembly fixture. That server is started through
    //   AspireTestApplication.Start, either by the fixture itself or by the owner host that the fixture borrows it from, so the
    //   port that the connection really reaches is the allocated one and lies in the same range. This half is what proves that
    //   a started container listens on the direct port, which the unstarted model cannot show.
    [Fact]
    public async Task Container_endpoints_use_direct_dynamic_ports_without_runtime_port_allocation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        var postgres = builder.AddPostgres("port-isolation-postgres").WithExplicitStart();

        // Act
        await using var app = await AspireTestApplication.Start(builder, [], null, ct);
        var endpoint = postgres.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        var connectionString = await fixture.GetServerConnectionString(ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Assert
        endpoint.IsProxied.ShouldBeFalse();
        endpoint.AllocatedEndpoint.ShouldNotBeNull().Port.ShouldBeInRange(10000, 32767);
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
