using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite;

/// <summary>
/// Composes a context for SQLite on a connection string, optionally with its own migrations history table and with
/// <see cref="SearchFold"/> registered on every connection the context opens.
/// </summary>
/// <remarks>
/// Register it with <see cref="DbContextConfigurationServiceCollectionExtensions.AddDbContextConfiguration{TContext}"/>, or call
/// <see cref="ConfigureOptions"/> from a design-time factory, so the application and the <c>dotnet ef</c> tools configure the context the
/// same way. Two contexts that share one database file each need their own <see cref="MigrationsHistoryTable"/> to migrate independently.
/// The search fold's model side stays with the context: call <see cref="SearchFoldModelBuilderExtensions.HasSearchFoldFunction"/> in its
/// <c>OnModelCreating</c>.
/// </remarks>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class SqliteDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly string _connectionString;

    /// <summary>Initializes a new instance of the <see cref="SqliteDbContextConfiguration{TContext}"/> class.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connectionString"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is empty or white space.</exception>
    public SqliteDbContextConfiguration(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = connectionString;
    }

    /// <summary>Gets the name of the migrations history table, or <see langword="null"/> for Entity Framework Core's default.</summary>
    public string? MigrationsHistoryTable { get; init; }

    /// <summary>Gets a value indicating whether every connection the context opens registers <see cref="SearchFold.FunctionName"/>.</summary>
    public bool UseSearchFold { get; init; }

    /// <inheritdoc />
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.UseSqlite(_connectionString, sqlite =>
        {
            if (MigrationsHistoryTable is not null)
            {
                sqlite.MigrationsHistoryTable(MigrationsHistoryTable);
            }
        });

        if (UseSearchFold)
        {
            optionsBuilder.AddSearchFold();
        }
    }
}
