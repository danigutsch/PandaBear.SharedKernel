using PandaBear.TestServices.Host;

namespace PandaBear.Npgsql.Tests;

/// <summary>Guards what the test-services host composes: one instance of every shared service, and Umami on the one PostgreSQL server.</summary>
public sealed class SharedTestServicesTests
{
    [Fact]
    public void The_host_composes_one_postgresql_server_and_one_umami_that_keeps_its_database_on_that_server()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        SharedTestServices.Add(builder);

        // Assert
        var server = builder.Resources.OfType<PostgresServerResource>().ShouldHaveSingleItem("every test that needs PostgreSQL borrows this one server");
        server.Name.ShouldBe(PostgreSqlTestServer.ResourceName);
        var umami = builder.Resources.OfType<UmamiResource>().ShouldHaveSingleItem();
        umami.Database.Parent.ShouldBeSameAs(server, "Umami keeps its data on the shared server instead of starting a second one");
        builder.Resources.OfType<ContainerResource>().Select(static container => container.Name).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe([PostgreSqlTestServer.ResourceName, "umami"], "the host starts exactly one container per service");
        builder.Resources.OfType<UmamiWebsiteResource>().ShouldHaveSingleItem().Umami.ShouldBeSameAs(umami);
    }

    [Fact]
    public void The_host_hands_over_the_variables_that_the_ci_configuration_lists_in_that_order()
    {
        // Act
        var variables = SharedTestServices.Variables;

        // Assert
        variables.ShouldBe(["SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING", "SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING"]);
    }
}
