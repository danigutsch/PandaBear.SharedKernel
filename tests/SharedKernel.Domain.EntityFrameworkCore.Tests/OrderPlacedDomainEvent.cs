
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal sealed record OrderPlacedDomainEvent(int OrderId) : IDomainEvent;
