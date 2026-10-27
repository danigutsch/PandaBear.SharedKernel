using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests registration and application of IDbContextConfiguration instances and the development diagnostics option.
/// </summary>
public sealed class DbContextConfigurationTests
{
    [Fact]
    public void Applies_registered_option_configurations_in_registration_order()
    {
        // Arrange
        var calls = new List<string>();
        var services = DbContextConfigurationTestServices.Create();
        services.AddDbContextConfiguration(new RecordingDbContextConfiguration(calls, "first"));
        services.AddDbContextConfiguration(new RecordingDbContextConfiguration(calls, "second"));
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: false);

        // Assert
        calls.ShouldBe(["first-options", "second-options"]);
    }

    [Fact]
    public void Configuration_exposes_the_target_context_type()
    {
        // Arrange
        IDbContextConfiguration<TestDbContext> configuration = new RecordingDbContextConfiguration([], "unused");

        // Act
        var contextType = configuration.ContextType;

        // Assert
        contextType.ShouldBe(typeof(TestDbContext));
    }

    [Fact]
    public void Apply_option_configurations_ignores_type_registrations_without_instances()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        services.AddSingleton<IDbContextConfiguration<TestDbContext>, ThrowingDbContextConfiguration>();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: false);

        // Assert
        options.Options.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public void Enables_development_diagnostics_for_every_context_when_requested()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: true);

        // Assert
        var coreOptions = options.Options.FindExtension<CoreOptionsExtension>();
        coreOptions.ShouldNotBeNull();
        coreOptions.IsSensitiveDataLoggingEnabled.ShouldBeTrue();
        coreOptions.DetailedErrorsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Leaves_development_diagnostics_off_by_default()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        services.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: false);

        // Assert
        var coreOptions = options.Options.FindExtension<CoreOptionsExtension>();
        var sensitiveDataLogging = coreOptions?.IsSensitiveDataLoggingEnabled ?? false;
        var detailedErrors = coreOptions?.DetailedErrorsEnabled ?? false;
        sensitiveDataLogging.ShouldBeFalse();
        detailedErrors.ShouldBeFalse();
    }

    [Fact]
    public void Add_configuration_rejects_missing_services()
    {
        // Arrange
        IServiceCollection? services = null;
        var configuration = new RecordingDbContextConfiguration([], "unused");

        Action addConfiguration = () => services!.AddDbContextConfiguration(configuration);

        // Act
        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Add_configuration_rejects_missing_configuration()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        IDbContextConfiguration<TestDbContext>? configuration = null;

        Action addConfiguration = () => services.AddDbContextConfiguration(configuration!);

        // Act
        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Apply_option_configurations_rejects_missing_services()
    {
        // Arrange
        IServiceCollection? services = null;
        var options = new DbContextOptionsBuilder<TestDbContext>();
        Action applyConfigurations = () => services!.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: false);

        // Act
        var exception = applyConfigurations.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Apply_option_configurations_rejects_missing_options()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        DbContextOptionsBuilder? options = null;
        Action applyConfigurations = () => services.ApplyDbContextOptionConfigurations<TestDbContext>(options!, enableDevelopmentDiagnostics: false);

        // Act
        var exception = applyConfigurations.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("options");
    }
}
