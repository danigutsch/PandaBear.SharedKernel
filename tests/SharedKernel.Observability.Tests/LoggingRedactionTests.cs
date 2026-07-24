using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
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
        const string email = "traveler@example.com";
        const string credential = "secret-token";
        const string sensitiveValue = "medical-note";
        const string financialValue = "payment-reference";
        TestCustomerLogger.LogImportedCustomer(logger, email, credential, sensitiveValue, financialValue, "completed");

        // Assert
        var message = provider.Messages.ShouldHaveSingleItem();
        message.ShouldContain("Imported customer", StringComparison.Ordinal);
        message.ShouldNotContain(email, StringComparison.Ordinal);
        message.ShouldNotContain(credential, StringComparison.Ordinal);
        message.ShouldNotContain(sensitiveValue, StringComparison.Ordinal);
        message.ShouldNotContain(financialValue, StringComparison.Ordinal);
        var structuredValues = provider.StructuredValues.Select(value => value.Value ?? string.Empty).ToArray();
        structuredValues.ShouldNotContain(email, StringComparer.Ordinal);
        structuredValues.ShouldNotContain(credential, StringComparer.Ordinal);
        structuredValues.ShouldNotContain(sensitiveValue, StringComparer.Ordinal);
        structuredValues.ShouldNotContain(financialValue, StringComparer.Ordinal);
        structuredValues.ShouldContain("completed", StringComparer.Ordinal);
    }

    [Fact]
    public void Privacy_taxonomy_exposes_only_technical_classifications()
    {
        // Act
        var classifications = new[]
        {
            PrivacyDataClassifications.Personal,
            PrivacyDataClassifications.Sensitive,
            PrivacyDataClassifications.Credential,
            PrivacyDataClassifications.Financial
        };

        // Assert
        classifications.ShouldAllSatisfy(classification => classification.TaxonomyName.ShouldBe("SharedKernel.Observability.Privacy"));
        classifications.Select(classification => classification.Value).ShouldBe([
            "Personal",
            "Sensitive",
            "Credential",
            "Financial"
        ]);
    }
}
