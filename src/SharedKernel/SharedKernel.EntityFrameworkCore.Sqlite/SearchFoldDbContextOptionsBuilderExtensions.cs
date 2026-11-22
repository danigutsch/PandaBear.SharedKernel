using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite;

/// <summary>Registers the search fold function on the connections of a context.</summary>
public static class SearchFoldDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds an interceptor that registers <see cref="SearchFold.FunctionName"/> on every SQLite connection the context opens. A
    /// connection that is already open when the context receives it needs <see cref="SearchFold.Register"/>.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <returns>The same options builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static DbContextOptionsBuilder AddSearchFold(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        return optionsBuilder.AddInterceptors(SearchFoldConnectionInterceptor.Instance);
    }

    /// <summary>
    /// Adds an interceptor that registers <see cref="SearchFold.FunctionName"/> on every SQLite connection the context opens. A
    /// connection that is already open when the context receives it needs <see cref="SearchFold.Register"/>.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <returns>The same options builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    public static DbContextOptionsBuilder<TContext> AddSearchFold<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        return optionsBuilder.AddInterceptors(SearchFoldConnectionInterceptor.Instance);
    }
}
