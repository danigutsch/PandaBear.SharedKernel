using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the Umami website resource: its place under Umami, its name-based website id, the connection string a site references, and its checks.</summary>
public sealed class UmamiWebsiteResourceTests
{
    [Fact]
    public void AddWebsite_adds_a_child_of_umami_with_its_domain_and_a_name_based_website_ID()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        var website = umami.AddWebsite("site", "example.com");

        // Assert
        website.Resource.Name.ShouldBe("site");
        website.Resource.Domain.ShouldBe("example.com");
        website.Resource.Umami.ShouldBeSameAs(umami.Resource);
        website.Resource.WebsiteId.ShouldBe(UmamiWebsiteIds.Create("umami", "site", "example.com"));
        builder.Resources.ShouldContain(website.Resource);
        var parent = website.Resource.Annotations.OfType<ResourceRelationshipAnnotation>().ShouldHaveSingleItem(annotation => annotation.Type == "Parent");
        parent.Resource.ShouldBeSameAs(umami.Resource);
    }

    [Fact]
    public void AddWebsite_waits_for_umami_before_it_is_set_up()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        var website = umami.AddWebsite("site", "example.com");

        // Assert
        var snapshot = website.Resource.Annotations.OfType<ResourceSnapshotAnnotation>().ShouldHaveSingleItem().InitialSnapshot;
        snapshot.State.ShouldNotBeNull().Text.ShouldBe(KnownResourceStates.Waiting);
    }

    [Fact]
    public void WithWebsiteId_uses_the_given_website_ID()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var website = builder.AddUmami("umami").AddWebsite("site", "example.com");
        var id = Guid.Parse("8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f");

        // Act
        website.WithWebsiteId(id);

        // Assert
        website.Resource.WebsiteId.ShouldBe(id);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("8f1d8a3e-7c2b-0d5e-9f60-1a2b3c4d5e6f")]
    [InlineData("8f1d8a3e-7c2b-4d5e-cf60-1a2b3c4d5e6f")]
    public void WithWebsiteId_refuses_an_ID_that_umami_does_not_accept(string id)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var website = builder.AddUmami("umami").AddWebsite("site", "example.com");

        // Act
        var set = () => website.WithWebsiteId(Guid.Parse(id));

        // Assert
        set.ShouldThrow<ArgumentException>().ParamName.ShouldBe("websiteId");
    }

    [Fact]
    public async Task A_referencing_site_receives_the_umami_endpoint_and_the_website_ID_as_its_connection_string()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");
        var website = umami.AddWebsite("site", "example.com");
        AllocatedEndpoints.Allocate(umami.Resource, UmamiResource.HttpEndpointName, 3300);
        var site = builder.AddExecutable("public", "dotnet", ".");

        // Act
        site.WithReference(website, connectionName: "umami");
        var variables = await EnvironmentVariables.Of(site.Resource);

        // Assert
        variables["ConnectionStrings__umami"].ShouldBe($"Endpoint=http://localhost:3300;WebsiteId={website.Resource.WebsiteId}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Example.com")]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("example")]
    public void AddWebsite_refuses_a_domain_that_umami_does_not_accept(string domain)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        var add = () => umami.AddWebsite("site", domain);

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("domain");
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("www.viajantes-turismo.com.br")]
    [InlineData("localhost")]
    [InlineData("localhost:5000")]
    public void AddWebsite_accepts_a_domain_that_umami_accepts(string domain)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        var website = umami.AddWebsite("site", domain);

        // Assert
        website.Resource.Domain.ShouldBe(domain);
    }

    [Fact]
    public void Umami_has_no_admin_password_parameter_unless_one_is_given()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");
        var password = builder.AddParameter("umami-admin-password", secret: true);

        // Act
        var before = umami.Resource.AdminPasswordParameter;
        umami.WithAdminPassword(password);

        // Assert
        before.ShouldBeNull("a fresh Umami signs in with its default password");
        umami.Resource.AdminPasswordParameter.ShouldBeSameAs(password.Resource);
    }

    [Fact]
    public void A_website_adds_no_volume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var umami = builder.AddUmami("umami");

        // Act
        umami.AddWebsite("site", "example.com");

        // Assert
        umami.Resource.Database.Parent.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty("the data is discarded with the containers unless WithDataVolume is called");
        umami.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }
}
