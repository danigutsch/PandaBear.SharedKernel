using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.EntityFrameworkCore.Npgsql;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Tests the PostgreSQL unique-violation parser without a database: it reads SQLSTATE 23505 with its constraint and table, wherever the
/// <see cref="PostgresException"/> sits in the inner-exception chain, and ignores every other failure.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class NpgsqlUniqueViolationParserTests
{
    private readonly NpgsqlUniqueViolationParser parser = new();

    [Fact]
    public void A_unique_violation_reports_its_constraint_and_table()
    {
        // Arrange
        var exception = new DbUpdateException("save failed", UniqueViolationTestExceptions.Postgres(PostgresErrorCodes.UniqueViolation, "IX_customers_email", "customers"));

        // Act
        var parsed = parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.ConstraintName.ShouldBe("IX_customers_email");
        violation.Table.ShouldBe("customers");
        violation.Columns.ShouldBeEmpty();
    }

    [Fact]
    public void A_unique_violation_wrapped_deeper_in_the_chain_is_found()
    {
        // Arrange
        var wrapped = new InvalidOperationException("batch failed", UniqueViolationTestExceptions.Postgres(PostgresErrorCodes.UniqueViolation, "PK_orders", "orders"));
        var exception = new DbUpdateException("save failed", wrapped);

        // Act
        var parsed = parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.ConstraintName.ShouldBe("PK_orders");
    }

    [Theory]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData(PostgresErrorCodes.NotNullViolation)]
    [InlineData(PostgresErrorCodes.ExclusionViolation)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    public void Another_postgres_error_is_not_a_unique_violation(string sqlState)
    {
        // Arrange
        var exception = new DbUpdateException("save failed", UniqueViolationTestExceptions.Postgres(sqlState, "IX_customers_email", "customers"));

        // Act
        var parsed = parser.TryParse(exception, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void A_unique_violation_without_a_constraint_name_is_not_reported()
    {
        // Arrange
        var exception = new DbUpdateException("save failed", UniqueViolationTestExceptions.Postgres(PostgresErrorCodes.UniqueViolation, constraintName: null, "customers"));

        // Act
        var parsed = parser.TryParse(exception, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void A_failure_without_a_postgres_exception_is_not_a_unique_violation()
    {
        // Arrange
        var exception = new DbUpdateException("save failed", new TimeoutException());

        // Act
        var parsed = parser.TryParse(exception, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void A_null_exception_is_refused()
    {
        // Act
        Action act = () => _ = parser.TryParse(null!, out _);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Registration_adds_the_parser_once_and_keeps_an_earlier_one()
    {
        // Arrange
        var services = new ServiceCollection();
        var earlier = new ServiceCollection().AddSingleton<IUniqueViolationParser, FixedTestParser>();

        // Act
        services.AddNpgsqlUniqueViolationParser().AddNpgsqlUniqueViolationParser();
        earlier.AddNpgsqlUniqueViolationParser();

        // Assert
        services.Where(descriptor => descriptor.ServiceType == typeof(IUniqueViolationParser)).ShouldHaveSingleItem()
            .ImplementationType.ShouldBe(typeof(NpgsqlUniqueViolationParser));
        earlier.Where(descriptor => descriptor.ServiceType == typeof(IUniqueViolationParser)).ShouldHaveSingleItem()
            .ImplementationType.ShouldBe(typeof(FixedTestParser));
    }
}
