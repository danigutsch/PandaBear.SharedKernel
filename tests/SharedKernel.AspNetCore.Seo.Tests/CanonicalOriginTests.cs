using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.Seo.Tests;

/// <summary>Tests AddCanonicalOrigin: the site's canonical origin comes from configuration, and a host with a bad one does not start.</summary>
public sealed class CanonicalOriginTests
{
    private const string Key = "PublicSite:Origin";

    [Fact]
    public async Task A_valid_origin_is_bound_and_builds_absolute_addresses()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[Key] = "https://example.com";
        builder.Services.AddCanonicalOrigin(Key);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var origin = host.Services.GetRequiredService<IOptions<CanonicalOriginOptions>>().Value;

        // Assert
        origin.Value.ShouldBe(new Uri("https://example.com/"));
        origin.Absolute("/tours/patagonia").ShouldBe(new Uri("https://example.com/tours/patagonia"));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com/shop")]
    [InlineData("https://example.com/?page=1")]
    [InlineData("https://user@example.com")]
    public async Task A_host_with_a_missing_or_invalid_origin_does_not_start_and_names_the_setting(string? value)
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[Key] = value;
        builder.Services.AddCanonicalOrigin(Key);
        using var host = builder.Build();

        // Act
        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var failure = await start.ShouldThrow<OptionsValidationException>();
        failure.Message.ShouldContain(Key, StringComparison.Ordinal);
    }

    [Fact]
    public void Absolute_refuses_a_path_that_is_not_root_relative()
    {
        // Arrange
        var origin = new CanonicalOriginOptions { Origin = "https://example.com" };

        // Act
        Action absolute = () => origin.Absolute("https://evil.example/tours");

        // Assert
        absolute.ShouldThrow<ArgumentException>().ParamName.ShouldBe("path");
    }

    [Fact]
    public void A_blank_configuration_key_is_refused()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action add = () => services.AddCanonicalOrigin(" ");

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("configurationKey");
    }
}
