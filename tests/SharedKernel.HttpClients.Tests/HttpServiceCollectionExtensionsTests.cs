namespace SharedKernel.HttpClients.Tests;

public sealed class HttpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHttpClientDefaults_registers_http_client_factory_defaults()
    {
        // Act
        var clientWasCreated = HttpClientDefaultsTestServices.CanCreateClient("shared-kernel-http-tests");

        // Assert
        clientWasCreated.ShouldBeTrue();
    }

    [Fact]
    public void AddHttpClientDefaults_throws_when_services_is_null()
    {
        // Act
        Action act = () => HttpServiceCollectionExtensions.AddHttpClientDefaults(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public void AddHttpClientDefaults_registers_metrics_without_tracing()
    {
        // Act
        var registrations = HttpClientDefaultsTestServices.GetTelemetryRegistrations();

        // Assert
        registrations.MetricsRegistered.ShouldBeTrue();
        registrations.TracingRegistered.ShouldBeFalse();
    }
}
