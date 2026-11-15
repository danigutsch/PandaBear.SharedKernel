using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>
/// Tests that the page-view settings and the Umami connection string are checked when the host starts, so a wrong setting stops the
/// host instead of losing page views, while a missing connection string only leaves the Umami target out.
/// </summary>
public sealed class PageViewOptionsTests
{
    private const string WebsiteId = "8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f";

    [Theory]
    [InlineData("http://secret.example:3000")]
    [InlineData("Endpoint=secret.example:3000;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=/secret;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=ftp://secret.example/;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=https://secret.example/?website=1;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=https://secret.example/#stats;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=https://admin:secret@umami.example/;WebsiteId=" + WebsiteId)]
    [InlineData("Endpoint=https://secret.example/")]
    [InlineData("WebsiteId=" + WebsiteId + ";Secret=1")]
    [InlineData("Endpoint=https://umami.example/;WebsiteId=secret")]
    [InlineData("Endpoint=https://secret.example/;WebsiteId=00000000-0000-0000-0000-000000000000")]
    [InlineData("Endpoint=\"https://secret.example/;WebsiteId=" + WebsiteId)]
    public async Task An_umami_connection_string_that_is_not_an_endpoint_and_a_website_ID_stops_the_host_without_repeating_it(string connectionString)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(connectionString);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(UmamiConnections.Key, StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret", StringComparison.OrdinalIgnoreCase);
        exception.Message.ShouldNotContain(connectionString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Endpoint=http://localhost:3000;WebsiteId=" + WebsiteId)]
    [InlineData("endpoint=https://analytics.example.com/umami/;websiteid=" + WebsiteId)]
    [InlineData(" Endpoint = https://analytics.example.com ; WebsiteId = {" + WebsiteId + "} ;")]
    public async Task An_umami_endpoint_and_website_ID_start_the_umami_target(string connectionString)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(connectionString);

        // Act
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var target = app.Services.GetRequiredService<PageViewQueues>().All.ShouldHaveSingleItem();
        target.Name.ShouldBe("umami");
    }

    [Fact]
    public async Task The_defaults_honour_privacy_signals_and_send_the_address_to_umami_as_x_forwarded_for()
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default);

        // Act
        await app.StartAsync(TestContext.Current.CancellationToken);
        var options = app.Services.GetRequiredService<IOptions<PageViewOptions>>().Value;
        var umami = app.Services.GetRequiredService<IOptionsMonitor<UmamiPageViewTargetOptions>>().Get(UmamiPageViewBuilderExtensions.DefaultConnectionName);

        // Assert
        options.HonorPrivacySignals.ShouldBeTrue();
        options.Hostname.ShouldBeNull();
        options.QueueCapacity.ShouldBe(1000);
        options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        umami.ClientIpHeader.ShouldBe("X-Forwarded-For");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task A_queue_capacity_outside_one_to_one_hundred_thousand_is_refused(int capacity)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default, options => options.QueueCapacity = capacity);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(PageViewOptions.QueueCapacity), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task A_shutdown_timeout_outside_zero_to_one_minute_is_refused(int seconds)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default, options => options.ShutdownTimeout = TimeSpan.FromSeconds(seconds));

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(PageViewOptions.ShutdownTimeout), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("X Forwarded For")]
    [InlineData("X-Client-IP:")]
    public async Task A_client_ip_header_that_is_not_a_header_name_is_refused(string header)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default, configureUmami: options => options.ClientIpHeader = header);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(UmamiPageViewTargetOptions.ClientIpHeader), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("exa mple.com")]
    public async Task A_hostname_that_is_not_a_host_name_is_refused(string hostname)
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default, options => options.Hostname = hostname);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<OptionsValidationException>();
        exception.Message.ShouldContain(nameof(PageViewOptions.Hostname), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_targets_with_the_same_name_stop_the_host()
    {
        // Arrange
        await using var app = OptionsTestHost.Build(UmamiConnections.Default, addTargets: pageViews => pageViews.AddTarget(_ => new RecordingPageViewTarget("umami")));

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = await start.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("umami", StringComparison.Ordinal);
    }

    [Fact]
    public void Use_without_add_names_the_missing_registration()
    {
        // Arrange
        var builder = TestWebHosts.CreateBuilder();
        using var app = builder.Build();

        // Act
        var use = () => app.UsePageViews();

        // Assert
        use.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(PageViewHostApplicationBuilderExtensions.AddPageViews), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_umami_connection_name_is_refused(string connectionName)
    {
        // Arrange
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var pageViews = builder.AddPageViews();

        // Act
        var add = () => pageViews.AddUmami(connectionName);

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("connectionName");
    }
}
