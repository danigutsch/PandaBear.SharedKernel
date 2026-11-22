using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite;

/// <summary>Maps the search fold function into a model.</summary>
public static class SearchFoldModelBuilderExtensions
{
    /// <summary>
    /// Maps <see cref="SearchFold.Of"/> to the SQLite function <see cref="SearchFold.FunctionName"/>, so a query such as
    /// <c>EF.Functions.Like(SearchFold.Of(entity.Name), pattern)</c> runs in SQL. Pass a value-converted column as its stored text with
    /// <c>SearchFold.Of((string)(object)entity.Code)</c>; Entity Framework Core drops that cast from the SQL.
    /// </summary>
    /// <param name="modelBuilder">The model builder, from <c>OnModelCreating</c>.</param>
    /// <returns>The same model builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder HasSearchFoldFunction(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDbFunction(() => SearchFold.Of(default)).HasName(SearchFold.FunctionName);
        return modelBuilder;
    }
}
