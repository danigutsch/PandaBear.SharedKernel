using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Testing.Assertions;

namespace SharedKernel.Observability.Tests;

public sealed partial class LoggingRedactionTests
{
    [Fact]
    public void Configure_opentelemetry_redacts_classified_log_parameters()
    {
        // Arrange
        var provider = new CapturingLoggerProvider();
        var builder = new HostApplicationBuilder();
        builder.ConfigureOpenTelemetry();
        builder.Logging.AddProvider(provider);

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<LoggingRedactionTests>>();

        // Act
        LogImportedCustomer(logger, "traveler@example.com");

        // Assert
        var message = provider.Messages.ShouldHaveSingleItem();
        message.ShouldContain("Imported customer", StringComparison.Ordinal);
        message.ShouldNotContain("traveler@example.com");
    }

    [LoggerMessage(1, LogLevel.Information, "Imported customer {Email}.")]
    private static partial void LogImportedCustomer(ILogger logger, [TestPersonalData] string email);
}
