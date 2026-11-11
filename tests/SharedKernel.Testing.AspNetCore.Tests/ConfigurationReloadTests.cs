using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>
/// Tests that the test hosts do not watch configuration files: one file watcher per file and host exhausts the per-user inotify limit when
/// many test hosts run in parallel.
/// </summary>
public sealed class ConfigurationReloadTests
{
    [Fact]
    public void A_web_application_test_host_does_not_reload_configuration_files()
    {
        // Arrange
        using var factory = WebApplicationTestHost.Create<Program>();

        // Act
        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        var files = configuration.Providers.OfType<FileConfigurationProvider>().ToArray();

        // Assert
        files.ShouldNotBeEmpty("the application reads appsettings files, so the check is not vacuous");
        files.ShouldAllSatisfy(provider => provider.Source.ReloadOnChange.ShouldBeFalse(), "a watched file costs an inotify instance");
    }

    [Fact]
    public async Task A_kestrel_test_host_does_not_reload_configuration_files()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>();

        // Act
        var configuration = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();
        var files = configuration.Providers.OfType<FileConfigurationProvider>().ToArray();

        // Assert
        files.ShouldNotBeEmpty("the application reads appsettings files, so the check is not vacuous");
        files.ShouldAllSatisfy(provider => provider.Source.ReloadOnChange.ShouldBeFalse(), "a watched file costs an inotify instance");
    }
}
