namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class FirstMessagingBusinessRecord(Guid id)
{
    public Guid Id { get; } = id;
}
