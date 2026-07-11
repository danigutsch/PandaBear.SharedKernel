using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

public sealed class LoggingRedactionTests
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
        TestCustomerLogger.LogImportedCustomer(logger, "traveler@example.com");

        // Assert
        var message = provider.Messages.ShouldHaveSingleItem();
        message.ShouldContain("Imported customer", StringComparison.Ordinal);
        message.ShouldNotContain("traveler@example.com", StringComparison.Ordinal);
    }
}
