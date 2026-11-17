using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the Umami resource builder: the pinned image, its PostgreSQL database, the generated app secret, the environment, the health check, data storage and the endpoint a site references.</summary>
public sealed class UmamiResourceTests
{
    [Fact]
    public void AddUmami_adds_a_container_with_a_pinned_image_an_HTTP_endpoint_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var umami = builder.AddUmami("umami");

        // Assert
        var image = umami.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        image.Registry.ShouldBe("docker.umami.is");
        image.Image.ShouldBe("umami-software/umami");
        image.SHA256.ShouldBe("e69d1a7932e9dab0fc523edc7be1f3a53f3c261f57856fc2167ae74a8fc022e8");
        var endpoint = umami.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        endpoint.Name.ShouldBe(UmamiResource.HttpEndpointName);
        endpoint.TargetPort.ShouldBe(3000);
        endpoint.UriScheme.ShouldBe("http");
        umami.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("umami_http_/api/heartbeat_200_check");
        umami.Resource.PrimaryEndpoint.EndpointName.ShouldBe(UmamiResource.HttpEndpointName);
    }

    [Fact]
    public void AddUmami_adds_a_pinned_postgresql_server_with_an_umami_database_and_waits_for_it()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var umami = builder.AddUmami("umami");

        // Assert
        var database = umami.Resource.Database;
        database.Name.ShouldBe("umami-db");
        database.DatabaseName.ShouldBe("umami");
        database.Parent.Name.ShouldBe("umami-postgres");
        var postgresImage = database.Parent.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        postgresImage.Registry.ShouldBe(PostgresContainerImageTags.Registry);
        postgresImage.Image.ShouldBe(PostgresContainerImageTags.Image);
        postgresImage.SHA256.ShouldBe(PostgresContainerImageTags.Digest);
        builder.Resources.ShouldContain(database);
        builder.Resources.ShouldContain(database.Parent);
        umami.Resource.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => ReferenceEquals(wait.Resource, database));
    }

    [Fact]
    public async Task AddUmami_generates_a_persistent_secret_app_secret_of_letters_and_digits()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var umami = builder.AddUmami("umami");
        var secret = await umami.Resource.AppSecretParameter.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        umami.Resource.AppSecretParameter.Name.ShouldBe("umami-app-secret");
        umami.Resource.AppSecretParameter.Secret.ShouldBeTrue();
        secret.ShouldNotBeNull().Length.ShouldBeGreaterThanOrEqualTo(64);
        secret.ShouldAllSatisfy(character => char.IsAsciiLetterOrDigit(character).ShouldBeTrue());
    }

    [Fact]
    public async Task AddUmami_configures_the_database_secret_binding_client_address_header_and_no_telemetry()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");
        AllocatedEndpoints.Allocate(umami.Resource.Database.Parent, "tcp", 5432);
        AllocatedEndpoints.AllocateInContainerNetwork(umami.Resource.Database.Parent, "tcp", "umami-postgres", 5432);

        // Act
        var variables = await EnvironmentVariables.Of(umami.Resource);

        // Assert
        var databaseUrl = new Uri(variables["DATABASE_URL"]);
        databaseUrl.Scheme.ShouldBe("postgresql");
        databaseUrl.UserInfo.ShouldStartWith("postgres:", StringComparison.Ordinal);
        databaseUrl.Host.ShouldBe("umami-postgres", "Umami reaches PostgreSQL in the container network");
        databaseUrl.Port.ShouldBe(5432);
        databaseUrl.AbsolutePath.ShouldBe("/umami");
        variables["APP_SECRET"].ShouldBe(await umami.Resource.AppSecretParameter.GetValueAsync(TestContext.Current.CancellationToken));
        variables["HOSTNAME"].ShouldBe("0.0.0.0");
        variables["CLIENT_IP_HEADER"].ShouldBe("X-Forwarded-For");
        variables["DISABLE_TELEMETRY"].ShouldBe("1");
    }

    [Fact]
    public async Task A_referencing_site_receives_the_umami_base_address_as_its_connection_string()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");
        AllocatedEndpoints.Allocate(umami.Resource, UmamiResource.HttpEndpointName, 3300);
        var site = builder.AddExecutable("site", "dotnet", ".");

        // Act
        site.WithReference(umami);
        var variables = await EnvironmentVariables.Of(site.Resource);

        // Assert
        variables["ConnectionStrings__umami"].ShouldBe("http://localhost:3300");
        var connectionString = await umami.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);
        connectionString.ShouldBe("http://localhost:3300");
    }

    [Fact]
    public void AddUmami_mounts_no_volume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var umami = builder.AddUmami("umami");

        // Assert
        umami.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
        umami.Resource.Database.Parent.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }

    [Fact]
    public void WithDataVolume_keeps_the_postgresql_data_in_a_named_volume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        umami.WithDataVolume("umami-data");

        // Assert
        var volume = umami.Resource.Database.Parent.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe("umami-data");
        volume.Target.ShouldBe("/var/lib/postgresql");
        volume.Type.ShouldBe(ContainerMountType.Volume);
        umami.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty("the Umami container keeps no state of its own");
    }

    [Fact]
    public void WithDataVolume_without_a_name_generates_one()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        umami.WithDataVolume();

        // Assert
        var volume = umami.Resource.Database.Parent.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldNotBeNull().ShouldContain("umami-postgres", StringComparison.Ordinal);
        volume.Type.ShouldBe(ContainerMountType.Volume);
    }

    [Fact]
    public void WithDataBindMount_keeps_the_postgresql_data_in_the_source_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");
        var source = Path.Combine(Path.GetTempPath(), "umami-data");

        // Act
        umami.WithDataBindMount(source);

        // Assert
        var mount = umami.Resource.Database.Parent.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        mount.Source.ShouldBe(source);
        mount.Target.ShouldBe("/var/lib/postgresql");
        mount.Type.ShouldBe(ContainerMountType.BindMount);
    }

    [Fact]
    public void WithHostPort_sets_the_host_port_of_the_dashboard_endpoint()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        umami.WithHostPort(3300);

        // Assert
        umami.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem().Port.ShouldBe(3300);
    }

    [Fact]
    public void AddUmami_on_an_existing_database_keeps_its_data_there_and_adds_no_server_of_its_own()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var server = builder.AddPostgres("shared-postgres");
        var database = server.AddDatabase("analytics", "umami");

        // Act
        var umami = builder.AddUmami("umami", database);

        // Assert
        umami.Resource.Database.ShouldBeSameAs(database.Resource);
        builder.Resources.OfType<PostgresServerResource>().ShouldHaveSingleItem().ShouldBeSameAs(server.Resource);
        builder.Resources.OfType<PostgresDatabaseResource>().ShouldHaveSingleItem().ShouldBeSameAs(database.Resource);
        umami.Resource.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => ReferenceEquals(wait.Resource, database.Resource));
        umami.Resource.AppSecretParameter.Name.ShouldBe("umami-app-secret");
    }

    [Fact]
    public async Task AddUmami_on_an_existing_database_gives_umami_the_URL_of_that_database_on_its_server()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var server = builder.AddPostgres("shared-postgres");
        var umami = builder.AddUmami("umami", server.AddDatabase("analytics", "umami_data"));
        AllocatedEndpoints.Allocate(server.Resource, "tcp", 5432);
        AllocatedEndpoints.AllocateInContainerNetwork(server.Resource, "tcp", "shared-postgres", 5432);

        // Act
        var variables = await EnvironmentVariables.Of(umami.Resource);

        // Assert
        var databaseUrl = new Uri(variables["DATABASE_URL"]);
        databaseUrl.Host.ShouldBe("shared-postgres", "Umami reaches the shared server in the container network");
        databaseUrl.AbsolutePath.ShouldBe("/umami_data");
    }

    [Fact]
    public void AddUmami_on_an_existing_database_refuses_a_data_volume_on_the_server_it_does_not_own()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var server = builder.AddPostgres("shared-postgres");
        var umami = builder.AddUmami("umami", server.AddDatabase("analytics"));

        // Act
        var keep = () => umami.WithDataVolume("umami-data");

        // Assert
        keep.ShouldThrow<InvalidOperationException>().Message.ShouldContain("shared-postgres", StringComparison.Ordinal);
        server.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty("the server's owner decides where its data lives");
    }

    [Fact]
    public void AddUmami_on_an_existing_database_refuses_a_data_bind_mount_on_the_server_it_does_not_own()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var server = builder.AddPostgres("shared-postgres");
        var umami = builder.AddUmami("umami", server.AddDatabase("analytics"));

        // Act
        var keep = () => umami.WithDataBindMount(Path.Combine(Path.GetTempPath(), "umami-data"));

        // Assert
        keep.ShouldThrow<InvalidOperationException>().Message.ShouldContain("shared-postgres", StringComparison.Ordinal);
        server.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty("the server's owner decides where its data lives");
    }

    [Fact]
    public void AddUmami_rejects_a_null_database()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var add = () => builder.AddUmami("umami", null!);

        // Assert
        add.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("database");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddUmami_rejects_a_blank_name(string name)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var add = () => builder.AddUmami(name);

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithDataBindMount_rejects_a_blank_source(string source)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        var mount = () => umami.WithDataBindMount(source);

        // Assert
        mount.ShouldThrow<ArgumentException>().ParamName.ShouldBe("source");
    }
}
