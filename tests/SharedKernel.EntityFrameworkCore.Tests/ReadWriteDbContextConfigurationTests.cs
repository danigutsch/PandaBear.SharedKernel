using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests the read and write DbContext configuration baselines and how they compose.
/// </summary>
public sealed class ReadWriteDbContextConfigurationTests
{
    [Fact]
    public async Task Read_baseline_makes_queries_no_tracking_by_default()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        _ = services.AddDbContextConfiguration(new ReadDbContextConfiguration<TestDbContext>());
        await using var provider = DbContextConfigurationTestServices.BuildInMemoryProvider(services);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var behavior = scope.ServiceProvider.GetRequiredService<TestDbContext>().ChangeTracker.QueryTrackingBehavior;

        // Assert
        behavior.ShouldBe(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public async Task Write_baseline_tracks_queries()
    {
        // Arrange
        var services = DbContextConfigurationTestServices.Create();
        _ = services.AddDbContextConfiguration(new WriteDbContextConfiguration<TestDbContext>());
        await using var provider = DbContextConfigurationTestServices.BuildInMemoryProvider(services);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var behavior = scope.ServiceProvider.GetRequiredService<TestDbContext>().ChangeTracker.QueryTrackingBehavior;

        // Assert
        behavior.ShouldBe(QueryTrackingBehavior.TrackAll);
    }

    [Theory]
    [InlineData(true, QueryTrackingBehavior.TrackAll)]
    [InlineData(false, QueryTrackingBehavior.NoTracking)]
    public async Task Baselines_compose_without_a_base_class_and_the_later_registration_wins(bool writeLast, QueryTrackingBehavior expected)
    {
        // Arrange
        var calls = new List<string>();
        var services = DbContextConfigurationTestServices.Create();
        if (writeLast)
        {
            _ = services.AddDbContextConfiguration(new ReadDbContextConfiguration<TestDbContext>());
            _ = services.AddDbContextConfiguration(new WriteDbContextConfiguration<TestDbContext>());
        }
        else
        {
            _ = services.AddDbContextConfiguration(new WriteDbContextConfiguration<TestDbContext>());
            _ = services.AddDbContextConfiguration(new ReadDbContextConfiguration<TestDbContext>());
        }

        _ = services.AddDbContextConfiguration(new RecordingDbContextConfiguration(calls, "own"));
        await using var provider = DbContextConfigurationTestServices.BuildInMemoryProvider(services);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var behavior = scope.ServiceProvider.GetRequiredService<TestDbContext>().ChangeTracker.QueryTrackingBehavior;

        // Assert
        behavior.ShouldBe(expected);
        calls.ShouldBe(["own-options"]);
    }

    [Fact]
    public void Baselines_configure_only_the_options_phase()
    {
        // Arrange
        IDbContextConfiguration<TestDbContext>[] baselines =
        [
            new ReadDbContextConfiguration<TestDbContext>(),
            new WriteDbContextConfiguration<TestDbContext>(),
        ];

        // Act
        var model = new ModelBuilder();
        foreach (var baseline in baselines)
        {
            baseline.ConfigureModel(model);
        }

        // Assert
        model.Model.GetEntityTypes().ShouldBeEmpty();
    }

    [Fact]
    public void Baselines_reject_missing_options()
    {
        // Arrange
        var read = new ReadDbContextConfiguration<TestDbContext>();
        var write = new WriteDbContextConfiguration<TestDbContext>();
        Action[] configure = [() => read.ConfigureOptions(null!), () => write.ConfigureOptions(null!)];

        // Act
        var exceptions = configure.Select(action => action.ShouldThrow<ArgumentNullException>()).ToArray();

        // Assert
        exceptions.Select(exception => exception.ParamName).ShouldBe(["optionsBuilder", "optionsBuilder"]);
    }
}
