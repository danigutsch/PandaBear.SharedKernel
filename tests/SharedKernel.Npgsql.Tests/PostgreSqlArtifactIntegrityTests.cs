using static PandaBear.Npgsql.Tests.PostgreSqlArtifactIntegrityTestData;

namespace PandaBear.Npgsql.Tests;

/// <summary>Exercises bounded integrity reports against a real PostgreSQL database.</summary>
/// <param name="fixture">The fixture that supplies each test's isolated database.</param>
public sealed class PostgreSqlArtifactIntegrityTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Reports_integrity_in_order_using_quoted_identifiers_and_exact_digest_comparison()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var setup = DataSource.CreateCommand(CreateTables + PopulateIntegrityCases);
        _ = await setup.ExecuteNonQueryAsync(ct);

        // Act
        var outcomes = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, null, 100, ct);

        // Assert
        outcomes.ShouldBe(
        [
            new(Revision(1), PostgreSqlArtifactIntegrityStatus.Intact),
            new(Revision(2), PostgreSqlArtifactIntegrityStatus.MissingBody),
            new(Revision(3), PostgreSqlArtifactIntegrityStatus.MissingIntegrityMetadata),
            new(Revision(4), PostgreSqlArtifactIntegrityStatus.LengthMismatch),
            new(Revision(5), PostgreSqlArtifactIntegrityStatus.LengthMismatch),
            new(Revision(6), PostgreSqlArtifactIntegrityStatus.DigestMismatch),
            new(Revision(7), PostgreSqlArtifactIntegrityStatus.DigestMismatch),
            new(Revision(8), PostgreSqlArtifactIntegrityStatus.MissingBody),
            new(Revision(10), PostgreSqlArtifactIntegrityStatus.MissingIntegrityMetadata),
            new(Revision(11), PostgreSqlArtifactIntegrityStatus.LengthMismatch),
        ]);
    }

    [Fact]
    public async Task Pages_eligible_rows_in_order_and_includes_the_empty_ID_on_the_first_page()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var setup = DataSource.CreateCommand(CreateTables + PopulatePage);
        _ = await setup.ExecuteNonQueryAsync(ct);

        // Act
        var first = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, null, 100, ct);
        var second = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, first[^1].Id, 100, ct);
        var end = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, second[^1].Id, 100, ct);
        var single = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, Revision(3), 1, ct);

        // Assert
        first.Select(static row => row.Id).ShouldBe(Enumerable.Range(0, 100).Select(static index => Revision(index * 2)));
        second.ShouldHaveSingleItem().ShouldBe(new(Revision(200), PostgreSqlArtifactIntegrityStatus.MissingBody));
        end.ShouldBeEmpty();
        single.ShouldHaveSingleItem().ShouldBe(new(Revision(4), PostgreSqlArtifactIntegrityStatus.MissingBody));
    }

    [Fact]
    public async Task Reads_inside_a_read_only_transaction_with_a_statement_timeout()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var setup = DataSource.CreateCommand(CreateTables + PopulateIntegrityCases + CreateReadOnlyView);
        _ = await setup.ExecuteNonQueryAsync(ct);
        var readOnlyMapping = Mapping with { MetadataTable = "Transaction settings" };

        // Act
        var outcomes = await PostgreSqlArtifactIntegrity.ReadPage(DataSource, readOnlyMapping, null, 1, ct);

        // Assert
        outcomes.ShouldHaveSingleItem().ShouldBe(new(Revision(1), PostgreSqlArtifactIntegrityStatus.Intact));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Rejects_a_page_size_outside_its_bounded_range(int pageSize)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        Func<Task> read = () => PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, null, pageSize, ct);
        var exception = await read.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("pageSize");
    }

    [Fact]
    public async Task Honors_an_already_canceled_page_request()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> read = () => PostgreSqlArtifactIntegrity.ReadPage(DataSource, Mapping, null, 100, cancellation.Token);
        var exception = await read.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("column\0name")]
    public async Task Rejects_an_invalid_identifier(string identifier)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var invalid = Mapping with { EligibilityColumn = identifier };

        // Act
        Func<Task> read = () => PostgreSqlArtifactIntegrity.ReadPage(DataSource, invalid, null, 1, ct);
        var exception = await read.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("identifier");
    }
}
