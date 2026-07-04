using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Tests;

public sealed class DbContextConfigurationTests
{
    [Fact]
    public void Applies_registered_option_configurations_in_registration_order()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddDbContextConfiguration(new RecordingDbContextConfiguration(calls, "first"));
        services.AddDbContextConfiguration(new RecordingDbContextConfiguration(calls, "second"));
        var options = new DbContextOptionsBuilder<TestDbContext>();

        services.ApplyDbContextOptionConfigurations<TestDbContext>(options);

        calls.ShouldBe(["first-options", "second-options"]);
    }

    [Fact]
    public void Configuration_exposes_the_target_context_type()
    {
        IDbContextConfiguration<TestDbContext> configuration = new RecordingDbContextConfiguration([], "unused");

        var contextType = configuration.ContextType;

        contextType.ShouldBe(typeof(TestDbContext));
    }

    [Fact]
    public void Apply_option_configurations_ignores_type_registrations_without_instances()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextConfiguration<TestDbContext>, ThrowingDbContextConfiguration>();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        services.ApplyDbContextOptionConfigurations<TestDbContext>(options);

        options.Options.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public void Enables_development_diagnostics_when_registered()
    {
        var services = new ServiceCollection();
        services.AddDbContextDevelopmentDiagnostics<TestDbContext>();
        var options = new DbContextOptionsBuilder<TestDbContext>();

        services.ApplyDbContextOptionConfigurations<TestDbContext>(options);

        var coreOptions = options.Options.FindExtension<CoreOptionsExtension>();
        coreOptions.ShouldNotBeNull();
        coreOptions.IsSensitiveDataLoggingEnabled.ShouldBeTrue();
        coreOptions.DetailedErrorsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Add_development_diagnostics_rejects_missing_services()
    {
        IServiceCollection? services = null;
        Action addDiagnostics = () => services!.AddDbContextDevelopmentDiagnostics<TestDbContext>();

        var exception = addDiagnostics.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Add_configuration_rejects_missing_services()
    {
        IServiceCollection? services = null;
        var configuration = new RecordingDbContextConfiguration([], "unused");

        Action addConfiguration = () => services!.AddDbContextConfiguration(configuration);

        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Add_configuration_rejects_missing_configuration()
    {
        var services = new ServiceCollection();
        IDbContextConfiguration<TestDbContext>? configuration = null;

        Action addConfiguration = () => services.AddDbContextConfiguration(configuration!);

        var exception = addConfiguration.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Apply_option_configurations_rejects_missing_services()
    {
        IServiceCollection? services = null;
        var options = new DbContextOptionsBuilder<TestDbContext>();
        Action applyConfigurations = () => services!.ApplyDbContextOptionConfigurations<TestDbContext>(options);

        var exception = applyConfigurations.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Apply_option_configurations_rejects_missing_options()
    {
        var services = new ServiceCollection();
        DbContextOptionsBuilder? options = null;
        Action applyConfigurations = () => services.ApplyDbContextOptionConfigurations<TestDbContext>(options!);

        var exception = applyConfigurations.ShouldThrow<ArgumentNullException>();

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Development_diagnostics_configuration_rejects_missing_builders()
    {
        var configuration = new DevelopmentDiagnosticsOptionsConfiguration<TestDbContext>();
        DbContextOptionsBuilder? options = null;
        ModelConfigurationBuilder? conventions = null;
        ModelBuilder? model = null;
        Action configureOptions = () => configuration.ConfigureOptions(options!);
        Action configureConventions = () => configuration.ConfigureConventions(conventions!);
        Action configureModel = () => configuration.ConfigureModel(model!);

        var optionsException = configureOptions.ShouldThrow<ArgumentNullException>();
        var conventionsException = configureConventions.ShouldThrow<ArgumentNullException>();
        var modelException = configureModel.ShouldThrow<ArgumentNullException>();

        optionsException.ParamName.ShouldBe("optionsBuilder");
        conventionsException.ParamName.ShouldBe("configurationBuilder");
        modelException.ParamName.ShouldBe("modelBuilder");
    }
}
