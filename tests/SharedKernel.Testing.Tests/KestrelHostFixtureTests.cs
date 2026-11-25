using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the shared application of a browser test class and what a browser test exposes before its application starts.</summary>
public sealed class KestrelHostFixtureTests
{
    [Fact]
    public async Task A_fixture_serves_the_application_with_its_environment_configuration_and_services()
    {
        // Arrange
        await using var fixture = new PingHostFixture();
        await fixture.InitializeAsync();
        using var client = new HttpClient { BaseAddress = fixture.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        text.ShouldBe("Staging|shared|hello from the app");
        fixture.Services.GetRequiredService<HostOwner>().ShouldBeSameAs(fixture.Owner);
        fixture.BaseAddress.Host.ShouldBe("127.0.0.1");
    }

    [Fact]
    public async Task A_fixture_that_has_not_started_has_no_address_or_services()
    {
        // Arrange
        await using var fixture = new KestrelHostFixture<Program>();
        Func<object?> address = () => fixture.BaseAddress;
        Func<object?> services = () => fixture.Services;

        // Act
        var addressFailure = address.ShouldThrow<InvalidOperationException>();
        var servicesFailure = services.ShouldThrow<InvalidOperationException>();

        // Assert
        addressFailure.Message.ShouldBe("The application has not started.");
        servicesFailure.Message.ShouldBe("The application has not started.");
    }

    [Fact]
    public void A_browser_test_that_has_not_started_has_no_address_or_services()
    {
        // Arrange
        Func<object?> address = () => new KestrelPageTestProbe().Address;
        Func<object?> services = () => new KestrelPageTestProbe().ApplicationServices;

        // Act
        var addressFailure = address.ShouldThrow<InvalidOperationException>();
        var servicesFailure = services.ShouldThrow<InvalidOperationException>();

        // Assert
        addressFailure.Message.ShouldBe("The application has not started.");
        servicesFailure.Message.ShouldBe("The application has not started.");
    }

    [Fact]
    public async Task A_browser_test_on_a_shared_application_that_has_not_started_has_no_services()
    {
        // Arrange
        await using var fixture = new KestrelHostFixture<Program>();
        Func<object?> services = () => new KestrelPageTestProbe(fixture).ApplicationServices;

        // Act
        var failure = services.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe("The application has not started.");
    }

    [Fact]
    public void A_browser_test_rejects_a_null_shared_application()
    {
        // Arrange
        Func<object?> create = () => new KestrelPageTestProbe(null!);

        // Act, Assert
        create.ShouldThrow<ArgumentNullException>();
    }
}
