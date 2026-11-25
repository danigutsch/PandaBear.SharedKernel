using System.Text;
using Microsoft.Data.Sqlite;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.EntityFrameworkCore.Sqlite;

/// <summary>
/// Accent- and case-insensitive contains search for SQLite. SQLite's <c>LIKE</c>, <c>lower()</c> and <c>NOCASE</c> fold only ASCII
/// letters, so <c>ESLOVÊNIA</c> does not find <c>Eslovênia</c>. A query compares search keys (<see cref="SearchKey"/>) instead:
/// <see cref="Of"/> computes the key of a column in SQL through the scalar function <see cref="FunctionName"/>, and
/// <see cref="ContainsPattern"/> builds the <c>LIKE</c> pattern from the user's input, with its wildcards escaped by
/// <see cref="EscapeCharacter"/>.
/// </summary>
/// <remarks>
/// The function runs once per row and cannot use an index, so a query reads the whole table: right for thousands of rows. For large
/// tables use an FTS5 table with the <c>trigram</c> tokenizer and <c>remove_diacritics 1</c>, which is indexed and case- and
/// accent-insensitive.
/// </remarks>
public static class SearchFold
{
    /// <summary>The name of the SQLite scalar function that computes the key in SQL.</summary>
    public const string FunctionName = "sharedkernel_search_fold";

    /// <summary>The escape character of the patterns <see cref="ContainsPattern"/> builds; pass it to <c>EF.Functions.Like</c>.</summary>
    public const string EscapeCharacter = SqlLikePattern.EscapeCharacter;

    /// <summary>
    /// Computes the search key of a text, <see cref="SearchKey.Of"/>. In an Entity Framework Core query mapped with
    /// <see cref="SearchFoldModelBuilderExtensions.HasSearchFoldFunction"/> it runs in SQL as <see cref="FunctionName"/>; elsewhere it
    /// runs in .NET with the same result.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The key; empty for <see langword="null"/>.</returns>
    public static string Of(string? text) => SearchKey.Of(text);

    /// <summary>
    /// Builds the <c>LIKE</c> pattern that finds the texts whose key contains the key of <paramref name="input"/>, for
    /// <c>EF.Functions.Like(SearchFold.Of(column), pattern, SearchFold.EscapeCharacter)</c>. The <c>%</c>, <c>_</c> and
    /// <c>\</c> the user typed are escaped, so they match themselves.
    /// </summary>
    /// <param name="input">The user's search text.</param>
    /// <returns>
    /// <c>%key%</c>, or <see langword="null"/> when the input has no letter or digit (including blank input), which should match
    /// nothing; whether blank input means "everything" instead is the caller's decision, made before calling this.
    /// </returns>
    public static string? ContainsPattern(string? input)
    {
        var key = SearchKey.Of(input);
        if (!key.EnumerateRunes().Any(Rune.IsLetterOrDigit))
        {
            return null;
        }

        return SqlLikePattern.ContainsLiteral(key);
    }

    /// <summary>
    /// Registers <see cref="FunctionName"/> on a connection as a deterministic function. <see cref="SearchFoldDbContextOptionsBuilderExtensions.AddSearchFold(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)"/>
    /// does this for every connection Entity Framework Core opens; call it yourself for a connection that is already open when the
    /// context receives it.
    /// </summary>
    /// <param name="connection">The connection, open or not.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public static void Register(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        connection.CreateFunction<string?, string>(FunctionName, Of, isDeterministic: true);
    }
}
