using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Qualifies the native archive client against disposable PostgreSQL databases.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture.</param>
public sealed class PostgreSqlTestArchiveNativeTests(PostgreSqlTestServerFixture fixture)
{
    [Fact(Explicit = true)]
    public async Task Native_restore_rolls_back_a_missing_owner_then_restores_exact_fixture_data()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var native = PostgreSqlTestArchiveNativeFixture.FromEnvironment();
        await using var bootstrap = await fixture.CreateDatabase(ct);
        await using var owner = await TemporaryPostgreSqlRole.Create(bootstrap.DataSource, ct);
        await using var source = await fixture.CreateDatabase(ct);
        await using var target = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("native-postgresql-archive");
        const string expected = "signed synthetic archive fixture";
        var client = new PostgreSqlTestArchiveClient(native.Start, 131_072, TimeSpan.FromSeconds(20));
        await PostgreSqlTestArchiveNativeFixture.CreateMarker(source.DataSource, ct);
        await PostgreSqlTestServerFixtureTestQueries.CreateOwnedTable(source.DataSource, expected, ct);
        await PostgreSqlTestArchiveNativeFixture.AssignOwner(source.DataSource, owner, ct);
        var archive = await client.Capture(source, workspace, ct);
        await source.DisposeAsync();
        await owner.DisposeAsync();

        // Act
        Func<Task> restore = () => client.Restore(archive, target, ct);
        var failure = await restore.ShouldThrow<InvalidOperationException>();
        var rolledBack = await PostgreSqlTestArchiveNativeFixture.TablesAreAbsent(target.DataSource, ct);
        await PostgreSqlTestArchiveNativeFixture.RecreateOwner(bootstrap.DataSource, owner, ct);
        await restore();
        var restoredMarker = await PostgreSqlTestArchiveNativeFixture.ReadMarker(target.DataSource, ct);
        var restored = await PostgreSqlTestServerFixtureTestQueries.ReadOwnedValue(target.DataSource, ct);
        var restoredOwner = await PostgreSqlTestArchiveNativeFixture.ReadOwner(target.DataSource, ct);

        // Assert
        var markerEntry = archive.TableOfContents.IndexOf("TABLE public archive_recovery_marker", StringComparison.Ordinal);
        var ownedEntry = archive.TableOfContents.IndexOf("TABLE public lease_owned_table", StringComparison.Ordinal);
        markerEntry.ShouldBeGreaterThanOrEqualTo(0);
        ownedEntry.ShouldBeGreaterThan(markerEntry);
        archive.TableOfContents.ShouldContain(owner.Name, StringComparison.Ordinal);
        failure.Message.ShouldContain("pg_restore", StringComparison.Ordinal);
        rolledBack.ShouldBeTrue();
        restoredMarker.ShouldBe(1);
        restored.ShouldBe(expected);
        restoredOwner.ShouldBe(owner.Name);
    }
}
