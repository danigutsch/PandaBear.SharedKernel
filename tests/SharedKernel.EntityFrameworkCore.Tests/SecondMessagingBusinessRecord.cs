namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Business record stored in the second_records table of the second_messaging schema by the second messaging context.
/// </summary>
/// <param name="id">Identifier of the record.</param>
internal sealed class SecondMessagingBusinessRecord(Guid id)
{
    /// <summary>
    /// Identifier of the record.
    /// </summary>
    public Guid Id { get; } = id;
}
