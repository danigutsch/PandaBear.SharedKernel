using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.UnitScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthMonitoringServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPostgreSqlIndexHealthMonitoring_registers_one_hosted_service_for_multiple_databases()
    {
        // Arrange
        var registration = PostgreSqlIndexHealthMonitoringRegistrationScope.Create(
        [
            "Host=localhost;Database=admin;Username=monitor;Password=test-only",
            "Host=localhost;Database=catalog;Username=monitor;Password=test-only",
        ]);

        // Act

        // Assert
        registration.HostedServiceCount.ShouldBe(1);
    }

    [Fact]
    public void AddPostgreSqlIndexHealthMonitoring_rejects_a_second_registration()
    {
        // Arrange
        var registration = PostgreSqlIndexHealthMonitoringRegistrationScope.Create(
        ["Host=localhost;Database=admin;Username=monitor;Password=test-only"]);

        // Act
        Action registerAgain = () => registration.Add(
            ["Host=localhost;Database=catalog;Username=monitor;Password=test-only"],
            new PostgreSqlIndexHealthMonitoringOptions());

        // Assert
        registerAgain.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddPostgreSqlIndexHealthMonitoring_does_not_expose_invalid_connection_string_values()
    {
        // Arrange
        const string sentinelSecret = "sentinel-secret-must-not-appear";
        Action register = () => PostgreSqlIndexHealthMonitoringRegistrationScope.Create(
            [$"Host=localhost;Password=ignored;{sentinelSecret}=true"]);

        // Act
        var exception = register.ShouldThrow<ArgumentException>();

        // Assert
        exception.ToString().ShouldNotContain(sentinelSecret, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPostgreSqlIndexHealthMonitoring_rejects_a_polling_interval_below_one_minute()
    {
        // Arrange
        Action register = () => PostgreSqlIndexHealthMonitoringRegistrationScope.Create(
            ["Host=localhost;Database=admin;Username=monitor;Password=test-only"],
            new PostgreSqlIndexHealthMonitoringOptions { PollingInterval = TimeSpan.FromSeconds(59) });

        // Act
        var exception = register.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("invalid polling interval", StringComparison.Ordinal);
    }

    [Fact]
    public void AddPostgreSqlIndexHealthMonitoring_rejects_a_command_timeout_above_five_minutes()
    {
        // Arrange
        Action register = () => PostgreSqlIndexHealthMonitoringRegistrationScope.Create(
            ["Host=localhost;Database=admin;Username=monitor;Password=test-only"],
            new PostgreSqlIndexHealthMonitoringOptions { CommandTimeout = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)) });

        // Act
        var exception = register.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("command timeout", StringComparison.Ordinal);
    }
}
