using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Looks at what a configuration did to a context.</summary>
public static class SqliteConfigurationProbe
{
    /// <summary>Gets the interceptors the options add.</summary>
    /// <param name="options">The options builder.</param>
    /// <returns>The interceptors.</returns>
    public static IEnumerable<IInterceptor> Interceptors(DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Options.FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];
    }

    /// <summary>Calls the search fold function in SQL on a connection the context opens.</summary>
    /// <param name="context">The context.</param>
    /// <param name="text">The text.</param>
    /// <returns>The folded text.</returns>
    public static async Task<string> Fold(DbContext context, string text)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await context.Database
            .SqlQuery<string>($"SELECT sharedkernel_search_fold({text}) AS Value")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Calls the search fold function in SQL and returns the SQLite error it raised.</summary>
    /// <param name="context">The context.</param>
    /// <param name="text">The text.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<Microsoft.Data.Sqlite.SqliteException> FoldFailure(DbContext context, string text)
    {
        try
        {
            await Fold(context, text);
        }
        catch (Microsoft.Data.Sqlite.SqliteException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The function call did not fail.");
    }
}
