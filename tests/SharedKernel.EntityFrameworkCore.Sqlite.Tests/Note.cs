namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A row of content whose changes bump <see cref="NoteRevision"/>; its text is unique.</summary>
public sealed class Note
{
    /// <summary>Gets or sets the key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the unique text.</summary>
    public string Text { get; set; } = "note";
}
