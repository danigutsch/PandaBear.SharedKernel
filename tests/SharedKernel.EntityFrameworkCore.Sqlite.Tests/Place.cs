namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A searchable entity with a plain name, an optional note and a value-converted code.</summary>
public sealed class Place
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional note, stored as NULL when absent.</summary>
    public string? Note { get; set; }

    /// <summary>Gets or sets the code, stored as its text through a value converter.</summary>
    public PlaceCode Code { get; set; } = new("none");
}
