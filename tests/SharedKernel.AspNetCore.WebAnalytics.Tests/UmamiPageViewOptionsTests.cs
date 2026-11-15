using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests that the Umami endpoint and the forwarder settings are checked when the host starts, so a wrong setting stops the host instead of losing page views.</summary>
public sealed class UmamiPageViewOptionsTests
{
    [Fact]
    public async Task A_host_without_the_umami_endpoint_does_not_start_and_names_the_setting()
    {
        // Arrange
        await using var app = OptionsTestHost.Build(endpoint: null);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(OptionsTestHost.EndpointKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("umami:3000")]
    [InlineData("/relative")]
    [InlineData("ftp://umami.example/")]
    [InlineData("https://umami.example/?website=1")]
    [InlineData("https://umami.example/#stats")]
    [InlineData("https://admin:secret@umami.example/")]
    public async Task An_endpoint_that_is_not_a_plain_web_base_address_is_refused(string endpoint)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(endpoint);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(OptionsTestHost.EndpointKey, StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("https://analytics.example.com/umami/")]
    public async Task A_plain_web_endpoint_starts(string endpoint)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(endpoint);

        // Act
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        app.Services.GetRequiredService<IOptions<UmamiPageViewOptions>>().Value.Endpoint.ShouldBe(new Uri(endpoint));
    }

    [Fact]
    public async Task The_defaults_honour_privacy_signals_and_send_the_address_as_x_forwarded_for()
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000");

        // Act
        await app.StartAsync(TestContext.Current.CancellationToken);
        var options = app.Services.GetRequiredService<IOptions<UmamiPageViewOptions>>().Value;

        // Assert
        options.HonorPrivacySignals.ShouldBeTrue();
        options.ClientIpHeader.ShouldBe("X-Forwarded-For");
        options.Hostname.ShouldBeNull();
        options.QueueCapacity.ShouldBe(1000);
        options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_configure_callback_runs_after_the_endpoint_is_read()
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000", options => options.Endpoint = new Uri("https://analytics.example.com/"));

        // Act
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        app.Services.GetRequiredService<IOptions<UmamiPageViewOptions>>().Value.Endpoint.ShouldBe(new Uri("https://analytics.example.com/"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task A_queue_capacity_outside_one_to_one_hundred_thousand_is_refused(int capacity)
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000", options => options.QueueCapacity = capacity);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(UmamiPageViewOptions.QueueCapacity), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task A_shutdown_timeout_outside_zero_to_one_minute_is_refused(int seconds)
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000", options => options.ShutdownTimeout = TimeSpan.FromSeconds(seconds));

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(UmamiPageViewOptions.ShutdownTimeout), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("X Forwarded For")]
    [InlineData("X-Client-IP:")]
    public async Task A_client_ip_header_that_is_not_a_header_name_is_refused(string header)
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000", options => options.ClientIpHeader = header);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(UmamiPageViewOptions.ClientIpHeader), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("exa mple.com")]
    public async Task A_hostname_that_is_not_a_host_name_is_refused(string hostname)
    {
        // Arrange
        await using var app = OptionsTestHost.Build("http://localhost:3000", options => options.Hostname = hostname);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(UmamiPageViewOptions.Hostname), StringComparison.Ordinal);
    }

    [Fact]
    public void Use_without_add_names_the_missing_registration()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        using var app = builder.Build();

        // Act
        var use = () => app.UseUmamiPageViews();

        // Assert
        use.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(UmamiPageViewServiceCollectionExtensions.AddUmamiPageViews), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_endpoint_configuration_key_is_refused(string key)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act
        var add = () => builder.Services.AddUmamiPageViews<FixedWebsiteIdProvider>(key);

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("endpointConfigurationKey");
    }
}
