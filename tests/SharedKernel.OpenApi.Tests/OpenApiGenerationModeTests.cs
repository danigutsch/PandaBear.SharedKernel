using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies trusted build-time OpenAPI generation detection.
/// </summary>
public sealed class OpenApiGenerationModeTests
{
    [Fact]
    public void Requires_the_generation_environment_and_document_generator_identity()
    {
        // Arrange
        var generationBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = OpenApiGenerationMode.HostEnvironmentName
        });
        var normalBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        // Act
        var trustedGeneration = OpenApiGenerationMode.IsEnabled(generationBuilder.Environment, "GetDocument.Insider");
        var ordinaryApplication = OpenApiGenerationMode.IsEnabled(generationBuilder.Environment, "ViajantesTurismo.Admin.ApiService");
        var missingEnvironment = OpenApiGenerationMode.IsEnabled(normalBuilder.Environment, "GetDocument.Insider");

        // Assert
        trustedGeneration.ShouldBeTrue();
        ordinaryApplication.ShouldBeFalse();
        missingEnvironment.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_enable_generation_for_the_current_test_process()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = OpenApiGenerationMode.HostEnvironmentName
        });

        // Act
        var isEnabled = OpenApiGenerationMode.IsEnabled(builder.Environment);

        // Assert
        isEnabled.ShouldBeFalse();
    }
}
