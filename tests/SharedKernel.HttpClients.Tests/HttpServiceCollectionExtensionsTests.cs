namespace SharedKernel.HttpClients.Tests;

[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
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
    public void AddHttpClientDefaults_registers_http_client_tracing_and_metrics()
    {
        // Act
        var registrations = HttpClientDefaultsTestServices.GetTelemetryRegistrations();

        // Assert
        registrations.MetricsRegistered.ShouldBeTrue();
        registrations.TracingRegistered.ShouldBeTrue();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("CONNECT")]
    public async Task AddHttpClientDefaults_does_not_retry_unsafe_requests(string method)
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendTransientAfterCommit(
            new HttpMethod(method),
            includeStreamingContent: false,
            TestContext.Current.CancellationToken);

        // Assert
        observation.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
        observation.AttemptCount.ShouldBe(1);
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public async Task AddHttpClientDefaults_does_not_retry_a_payment_request_after_streaming_content_is_sent()
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendTransientAfterCommit(
            HttpMethod.Post,
            includeStreamingContent: true,
            TestContext.Current.CancellationToken);

        // Assert
        observation.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
        observation.AttemptCount.ShouldBe(1);
        observation.ContentSerializationCount.ShouldBe(1);
        observation.SourceBytesRead.ShouldBe(HttpClientDefaultsTestServices.StreamingPayloadLength);
    }

    [Fact]
    public async Task AddHttpClientDefaults_preserves_safe_get_retries()
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendTransientAfterCommit(
            HttpMethod.Get,
            includeStreamingContent: false,
            TestContext.Current.CancellationToken);

        // Assert
        observation.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        observation.AttemptCount.ShouldBe(2);
    }
}
