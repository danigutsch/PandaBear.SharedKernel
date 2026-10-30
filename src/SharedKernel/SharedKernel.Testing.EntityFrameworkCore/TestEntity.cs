namespace PandaBear.Testing.EntityFrameworkCore;

/// <summary>A simple entity with an id and a name, stored by <see cref="TestDbContext" />.</summary>
public sealed class TestEntity
{
    /// <summary>Gets or sets the database-generated identifier.</summary>
    public int Id
    {
        get; set;
    }

    /// <summary>Gets or sets the name of the entity, empty by default.</summary>
    public string Name { get; set; } = string.Empty;
}
