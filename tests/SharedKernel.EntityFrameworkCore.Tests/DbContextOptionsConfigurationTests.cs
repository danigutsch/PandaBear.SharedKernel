using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Tests;

public sealed class DbContextOptionsConfigurationTests
{
    [Fact]
    public void Applies_registered_configurations_in_registration_order()
    {
        // Arrange
        var calls = new List<string>();
        var services = DbContextOptionsConfigurationTestServices.Create();
        services.AddDbContextOptionsConfiguration(new RecordingOptionsConfiguration(calls, "first"));
        services.AddDbContextOptionsConfiguration(new RecordingOptionsConfiguration(calls, "second"));
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionsConfigurations<TestDbContext>(options);

        // Assert
        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Enables_development_diagnostics_when_registered()
    {
        // Arrange
        var services = DbContextOptionsConfigurationTestServices.Create();
        services.AddDbContextDevelopmentDiagnostics<TestDbContext>();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionsConfigurations<TestDbContext>(options);

        // Assert
        var coreOptions = options.Options.FindExtension<CoreOptionsExtension>();
        coreOptions.ShouldNotBeNull();
        coreOptions.IsSensitiveDataLoggingEnabled.ShouldBeTrue();
        coreOptions.DetailedErrorsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Add_configuration_rejects_missing_services()
    {
        // Arrange
        IServiceCollection? services = null;
        var configuration = new RecordingOptionsConfiguration([], "unused");

        Action addConfiguration = () => services!.AddDbContextOptionsConfiguration(configuration);

        // Act
        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Add_configuration_rejects_missing_configuration()
    {
        // Arrange
        var services = DbContextOptionsConfigurationTestServices.Create();
        IDbContextOptionsConfiguration<TestDbContext>? configuration = null;

        Action addConfiguration = () => services.AddDbContextOptionsConfiguration(configuration!);

        // Act
        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("configuration");
    }
}
