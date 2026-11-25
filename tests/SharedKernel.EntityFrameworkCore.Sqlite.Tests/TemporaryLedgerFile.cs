using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A unique SQLite file path for <see cref="LedgerDbContext"/>, deleted on dispose; the file itself is created only by a writer.</summary>
public sealed class TemporaryLedgerFile : IDisposable
{
    /// <summary>Gets the absolute path of the database file.</summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ledger-{Guid.NewGuid():N}.db");

    /// <summary>Creates a service provider whose context opens the file in the given mode, without connection pooling.</summary>
    /// <param name="mode">The open mode.</param>
    /// <returns>The provider.</returns>
    public ServiceProvider Services(SqliteOpenMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = Path, Mode = mode, Pooling = false }.ToString();
        return new ServiceCollection()
            .AddDbContext<LedgerDbContext>(options => options.UseSqlite(connectionString))
            .BuildServiceProvider();
    }

    /// <inheritdoc />
    public void Dispose() => File.Delete(Path);
}
