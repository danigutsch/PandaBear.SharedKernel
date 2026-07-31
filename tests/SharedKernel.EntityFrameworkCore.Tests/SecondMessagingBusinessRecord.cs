namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class SecondMessagingBusinessRecord(Guid id)
{
    public Guid Id { get; } = id;
}
