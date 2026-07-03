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
}
