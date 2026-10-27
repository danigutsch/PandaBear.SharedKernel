namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Simple entity with an id and a name used by the transaction scope and configuration tests.
/// </summary>
internal sealed class TestEntity
{
    /// <summary>
    /// Database-generated identifier.
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// Name of the entity, empty by default.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
