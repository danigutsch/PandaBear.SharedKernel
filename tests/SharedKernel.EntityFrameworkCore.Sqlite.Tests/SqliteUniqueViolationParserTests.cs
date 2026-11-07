using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests how SQLite error text becomes a <see cref="UniqueViolation"/>.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "UniqueViolations")]
public sealed class SqliteUniqueViolationParserTests
{
    private static readonly SqliteUniqueViolationParser Parser = new();

    [Fact]
    public void A_single_column_violation_reports_table_and_column()
    {
        // Arrange
        var exception = SqliteErrors.Wrap(19, 2067, "SQLite Error 19: 'UNIQUE constraint failed: Customers.Email'.");

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.ConstraintName.ShouldBeNull();
        violation.Table.ShouldBe("Customers");
        violation.Columns.ShouldBe(["Email"]);
    }

    [Fact]
    public void A_composite_violation_reports_every_column()
    {
        // Arrange
        var exception = SqliteErrors.Wrap(19, 2067, "SQLite Error 19: 'UNIQUE constraint failed: Accounts.TenantId, Accounts.Code'.");

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.Table.ShouldBe("Accounts");
        violation.Columns.ShouldBe(["TenantId", "Code"]);
    }

    [Fact]
    public void An_expression_index_violation_reports_the_index_name()
    {
        // Arrange
        var exception = SqliteErrors.Wrap(19, 2067, "SQLite Error 19: 'UNIQUE constraint failed: index 'IX_Customers_Lower'.'.");

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.ConstraintName.ShouldBe("IX_Customers_Lower");
        violation.Table.ShouldBeNull();
        violation.Columns.ShouldBeEmpty();
    }

    [Fact]
    public void A_primary_key_violation_is_recognized()
    {
        // Arrange
        var exception = SqliteErrors.Wrap(19, 1555, "SQLite Error 19: 'UNIQUE constraint failed: Customers.Id'.");

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.Columns.ShouldBe(["Id"]);
    }

    [Fact]
    public void A_violation_nested_deeper_in_the_exception_chain_is_found()
    {
        // Arrange
        var sqlite = new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: Customers.Email'.", 19, 2067);
        var exception = new DbUpdateException("save failed", new InvalidOperationException("wrapper", sqlite));

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeTrue();
        violation.Table.ShouldBe("Customers");
    }

    [Theory]
    [InlineData(19, 787)]
    [InlineData(19, 1299)]
    [InlineData(5, 5)]
    [InlineData(1, 1)]
    public void Other_sqlite_errors_are_not_unique_violations(int errorCode, int extendedCode)
    {
        // Arrange
        var exception = SqliteErrors.Wrap(errorCode, extendedCode, "SQLite Error: 'UNIQUE constraint failed: Customers.Email'.");

        // Act
        var parsed = Parser.TryParse(exception, out var violation);

        // Assert
        parsed.ShouldBeFalse();
        violation.Columns.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SQLite Error 19: 'constraint failed'.")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: NoDot'.")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: Customers.'.")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: index '.")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: '.")]
    public void Unrecognizable_messages_are_not_reported(string message)
    {
        // Arrange
        var exception = SqliteErrors.Wrap(19, 2067, message);

        // Act
        var parsed = Parser.TryParse(exception, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void Exceptions_without_a_sqlite_cause_are_not_unique_violations()
    {
        // Act
        var none = Parser.TryParse(new DbUpdateException("save failed"), out _);
        var other = Parser.TryParse(new DbUpdateException("save failed", new InvalidOperationException()), out _);

        // Assert
        none.ShouldBeFalse();
        other.ShouldBeFalse();
    }

    [Fact]
    public void A_null_exception_is_rejected()
    {
        // Arrange
        Action act = () => _ = Parser.TryParse(null!, out _);

        // Act & Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
