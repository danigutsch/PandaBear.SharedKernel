using Npgsql;
using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.IntegrationScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthCollectorIntegrationTests
{
    [Fact]
    public async Task Collect_returns_unavailable_without_assessments_when_the_database_is_unreachable()
    {
        // Arrange
        using var dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=postgres;Username=monitor;Password=test-only;Timeout=1");
        var collector = new PostgreSqlIndexHealthCollector(dataSource, TimeSpan.FromSeconds(1));

        // Act
        var result = await collector.Collect(TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.ShouldBe(PostgreSqlIndexHealthCollectionOutcome.Unavailable);
        result.Assessments.ShouldBeEmpty();
    }
}
