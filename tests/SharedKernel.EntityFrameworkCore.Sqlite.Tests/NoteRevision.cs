namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>The single revision row of <see cref="NoteDbContext"/>.</summary>
public sealed class NoteRevision
{
    /// <summary>The id of the single row.</summary>
    public const int SingletonId = 1;

    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the revision.</summary>
    public long Value { get; set; }
}
