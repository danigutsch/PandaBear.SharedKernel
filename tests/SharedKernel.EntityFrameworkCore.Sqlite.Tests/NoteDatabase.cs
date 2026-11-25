using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A private in-memory database of notes with its revision row at 0.</summary>
public sealed class NoteDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    /// <summary>Opens the database and creates the schema.</summary>
    public NoteDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a context on the shared connection.</summary>
    /// <returns>A new context.</returns>
    public NoteDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NoteDbContext>().UseSqlite(_connection).Options);

    /// <summary>Reads the current revision through a new context.</summary>
    /// <returns>The revision.</returns>
    public async Task<long> Revision()
    {
        await using var context = CreateContext();
        return await context.Revisions.AsNoTracking().Select(row => row.Value).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Reads the texts of the stored notes through a new context.</summary>
    /// <returns>The texts, in order.</returns>
    public async Task<List<string>> Texts()
    {
        await using var context = CreateContext();
        return await context.Notes.AsNoTracking().Select(note => note.Text).OrderBy(text => text).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Stores a note through a new context.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The key of the note.</returns>
    public async Task<Guid> Store(string text)
    {
        await using var context = CreateContext();
        var note = new Note { Text = text };
        context.Notes.Add(note);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return note.Id;
    }

    /// <summary>Drops the revision table, so the next bump fails.</summary>
    public void DropRevisionTable()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DROP TABLE Revisions";
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
