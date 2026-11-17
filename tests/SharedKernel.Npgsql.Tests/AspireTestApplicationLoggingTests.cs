using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Tests where an Aspire test application logs: started from a test, to the test's output, which the runner shows only for a failing
/// test, instead of the process console, which <c>dotnet test</c> streams for every test; started outside any test, such as by a process
/// that serves the services tests borrow, to the console as before.
/// </summary>
public sealed class AspireTestApplicationLoggingTests
{
    [Fact]
    public async Task An_application_started_from_a_test_logs_to_its_output_instead_of_the_console()
    {
        // Arrange
        var builder = AspireTestApplication.CreateBuilder();
        var probe = new LoggingProbe();
        builder.Services.AddSingleton<IHostedService>(services => probe.Bind(services));

        // Act
        await using var app = await AspireTestApplication.Start(builder, [], null, TestContext.Current.CancellationToken);

        // Assert
        probe.Providers.ShouldNotContain(provider => provider is ConsoleLoggerProvider);
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain(LoggingProbe.Entry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_application_started_outside_any_test_keeps_the_console()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var probe = new LoggingProbe();

        // Act
        Task<AspireTestApplication> start;
        using (ExecutionContext.SuppressFlow())
        {
            start = Task.Run(
                async () =>
                {
                    var builder = AspireTestApplication.CreateBuilder();
                    builder.Configuration["Logging:LogLevel:Default"] = "Warning";
                    builder.Services.AddSingleton<IHostedService>(services => probe.Bind(services));
                    return await AspireTestApplication.Start(builder, [], null, ct);
                },
                ct);
        }

        await using var app = await start;

        // Assert
        probe.Providers.ShouldContain(provider => provider is ConsoleLoggerProvider);
    }
}
