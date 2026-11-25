using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace SharedKernel.Testing.AspNetCore.Tests;

/// <summary>
/// Tests that the test hosts log to the output of the test that runs them, which the runner shows only for a failing test, and not to the
/// process console, which <c>dotnet test</c> streams for every test.
/// </summary>
public sealed class TestOutputLoggingTests
{
    [Fact]
    public async Task A_web_application_test_host_logs_to_the_output_of_its_test_instead_of_the_console()
    {
        // Arrange
        using var factory = WebApplicationTestHost.Create<Program>();
        using var client = factory.CreateClient();

        // Act
        _ = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        factory.Services.GetServices<ILoggerProvider>().ShouldNotContain(provider => provider is ConsoleLoggerProvider);
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain("Request starting HTTP/1.1 GET http://localhost/ping", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_kestrel_test_host_logs_requests_from_a_real_client_to_the_output_of_its_test_instead_of_the_console()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>();
        using var client = new HttpClient();
        var ping = new Uri(host.BaseAddress, "ping");

        // Act
        _ = await client.GetStringAsync(ping, TestContext.Current.CancellationToken);

        // Assert
        host.Services.GetServices<ILoggerProvider>().ShouldNotContain(provider => provider is ConsoleLoggerProvider);
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain($"Request starting HTTP/1.1 GET {ping}", StringComparison.Ordinal);
    }
}
