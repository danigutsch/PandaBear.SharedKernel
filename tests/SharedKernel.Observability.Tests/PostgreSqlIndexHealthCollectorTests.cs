using Npgsql;
using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.UnitScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthCollectorTests
{
    [Fact]
    public void Constructor_rejects_a_zero_command_timeout()
    {
        // Arrange
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=postgres");
        Action createCollector = () => _ = new PostgreSqlIndexHealthCollector(dataSource, TimeSpan.Zero);

        // Act
        var exception = createCollector.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("commandTimeout");
    }

    [Fact]
    public void Constructor_rejects_a_command_timeout_above_five_minutes()
    {
        // Arrange
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=postgres");
        Action createCollector = () => _ = new PostgreSqlIndexHealthCollector(
            dataSource,
            TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        // Act
        var exception = createCollector.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("commandTimeout");
    }

    [Fact]
    public async Task Collect_rethrows_cooperative_cancellation()
    {
        // Arrange
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=postgres");
        var collector = new PostgreSqlIndexHealthCollector(dataSource);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> collect = () => collector.Collect(cancellation.Token).AsTask();

        // Act
        var exception = await collect.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

}
