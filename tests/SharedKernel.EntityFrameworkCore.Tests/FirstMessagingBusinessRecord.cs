namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Business record stored in the first_records table of the first_messaging schema by the first messaging context.
/// </summary>
/// <param name="id">Identifier of the record.</param>
internal sealed class FirstMessagingBusinessRecord(Guid id)
{
    /// <summary>
    /// Identifier of the record.
    /// </summary>
    public Guid Id { get; } = id;
}
