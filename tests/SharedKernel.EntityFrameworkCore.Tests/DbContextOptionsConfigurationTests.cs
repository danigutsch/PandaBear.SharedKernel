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
        Assert.Equal(["first", "second"], calls);
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
        Assert.NotNull(coreOptions);
        Assert.True(coreOptions.IsSensitiveDataLoggingEnabled);
        Assert.True(coreOptions.DetailedErrorsEnabled);
    }

    [Fact]
    public void Add_configuration_rejects_missing_services()
    {
        // Arrange
        IServiceCollection? services = null;
        var configuration = new RecordingOptionsConfiguration([], "unused");

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services!.AddDbContextOptionsConfiguration(configuration));

        // Assert
        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void Add_configuration_rejects_missing_configuration()
    {
        // Arrange
        var services = DbContextOptionsConfigurationTestServices.Create();
        IDbContextOptionsConfiguration<TestDbContext>? configuration = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddDbContextOptionsConfiguration(configuration!));

        // Assert
        Assert.Equal("configuration", exception.ParamName);
    }
}
