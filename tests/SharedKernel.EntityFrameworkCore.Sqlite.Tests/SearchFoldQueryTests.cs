using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests accent- and case-insensitive contains search against a real SQLite database.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "SearchFold")]
public sealed class SearchFoldQueryTests
{
    private static readonly Place[] Places =
    [
        new() { Id = 1, Name = "Eslovênia", Note = "Liubliana e o lago Bled", Code = new PlaceCode("SI-LJU") },
        new() { Id = 2, Name = "Eslováquia", Note = null, Code = new PlaceCode("SK-BTS") },
        new() { Id = 3, Name = "São  Paulo 100%", Note = "garoa", Code = new PlaceCode("BR-SAO") },
    ];

    [Theory]
    [InlineData("ESLOVÊNIA", new[] { 1 })]
    [InlineData("eslovenia", new[] { 1 })]
    [InlineData("  Eslov  ", new[] { 1, 2 })]
    [InlineData("  sao   PAULO ", new[] { 3 })]
    [InlineData("100%", new[] { 3 })]
    [InlineData("quia", new[] { 2 })]
    public async Task Names_are_found_whatever_the_accents_case_and_spacing_of_the_input(string input, int[] expected)
    {
        // Arrange
        using var database = new PlaceDatabase(Places);
        await using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern(input);

        // Act
        var found = await context.Places
            .Where(place => EF.Functions.Like(SearchFold.Of(place.Name), pattern, SearchFold.EscapeCharacter))
            .OrderBy(place => place.Id)
            .Select(place => place.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe([.. expected]);
    }

    [Theory]
    [InlineData("eslov%nia")]
    [InlineData("eslov_nia")]
    [InlineData("e%a")]
    [InlineData("1_0")]
    public async Task Wildcard_characters_in_the_input_never_act_as_wildcards(string input)
    {
        // Arrange
        using var database = new PlaceDatabase(Places);
        await using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern(input);

        // Act
        var found = await context.Places
            .Where(place => EF.Functions.Like(SearchFold.Of(place.Name), pattern, SearchFold.EscapeCharacter))
            .CountAsync(TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe(0);
    }

    [Fact]
    public async Task A_folded_name_matches_literal_metacharacters()
    {
        // Arrange
        using var database = new PlaceDatabase([
            new() { Id = 1, Name = @"Viagem em 山 Á_B%\", Code = new PlaceCode("BR-1") },
            new() { Id = 2, Name = @"Viagem em 山 ÁxBz\", Code = new PlaceCode("BR-2") },
        ]);
        await using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern(@"  山 A_B%\  ");

        // Act
        var found = await context.Places
            .Where(place => EF.Functions.Like(SearchFold.Of(place.Name), pattern, SearchFold.EscapeCharacter))
            .Select(place => place.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe([1]);
    }

    [Fact]
    public async Task A_null_column_matches_nothing_and_does_not_fail()
    {
        // Arrange
        using var database = new PlaceDatabase(Places);
        await using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern("a");

        // Act
        var found = await context.Places
            .Where(place => EF.Functions.Like(SearchFold.Of(place.Note), pattern, SearchFold.EscapeCharacter))
            .OrderBy(place => place.Id)
            .Select(place => place.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe([1, 3]);
    }

    [Fact]
    public async Task A_value_converted_column_is_searched_through_its_stored_text()
    {
        // Arrange
        using var database = new PlaceDatabase(Places);
        await using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern("sk-bts");

        // Act
        var found = await context.Places
            .Where(place => EF.Functions.Like(SearchFold.Of((string)(object)place.Code), pattern, SearchFold.EscapeCharacter))
            .Select(place => place.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe([2]);
    }

    [Fact]
    public void The_function_runs_in_SQL_rather_than_on_the_client()
    {
        // Arrange
        using var database = new PlaceDatabase(Places);
        using var context = database.CreateContext();
        var pattern = SearchFold.ContainsPattern("eslovenia");

        // Act
        var sql = context.Places.Where(place => EF.Functions.Like(SearchFold.Of(place.Name), pattern, SearchFold.EscapeCharacter)).ToQueryString();

        // Assert
        sql.ShouldContain(SearchFold.FunctionName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_connection_opened_before_the_context_gets_the_function_by_explicit_registration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        SearchFold.Register(connection);
        await using var context = new PlaceDbContext(new DbContextOptionsBuilder<PlaceDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Places.AddRange(new Place { Id = 1, Name = "Eslovênia" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var pattern = SearchFold.ContainsPattern("ESLOVÊNIA");

        // Act
        var found = await context.Places.CountAsync(place => EF.Functions.Like(SearchFold.Of(place.Name), pattern, SearchFold.EscapeCharacter), TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe(1);
    }

    [Fact]
    public async Task The_bundled_database_engine_offers_indexed_trigram_search_without_diacritics_for_large_tables()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE VIRTUAL TABLE places USING fts5(name, tokenize = 'trigram remove_diacritics 1'); "
            + "INSERT INTO places (name) VALUES ('Eslovênia'), ('Eslováquia'), ('Brasil');";
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT count(*) FROM places WHERE places MATCH '\"ESLOVE\"'";

        // Act
        var found = (long)(await query.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        // Assert
        Version.Parse(connection.ServerVersion).ShouldBeGreaterThanOrEqualTo(new Version(3, 45));
        found.ShouldBe(1);
    }

    [Fact]
    public void Missing_builders_and_connections_are_rejected()
    {
        // Arrange
        Action noOptions = () => SearchFoldDbContextOptionsBuilderExtensions.AddSearchFold(null!);
        Action noTypedOptions = () => SearchFoldDbContextOptionsBuilderExtensions.AddSearchFold<PlaceDbContext>(null!);
        Action noModel = () => SearchFoldModelBuilderExtensions.HasSearchFoldFunction(null!);
        Action noConnection = () => SearchFold.Register(null!);

        // Act & Assert
        noOptions.ShouldThrow<ArgumentNullException>();
        noTypedOptions.ShouldThrow<ArgumentNullException>();
        noModel.ShouldThrow<ArgumentNullException>();
        noConnection.ShouldThrow<ArgumentNullException>();
    }
}
