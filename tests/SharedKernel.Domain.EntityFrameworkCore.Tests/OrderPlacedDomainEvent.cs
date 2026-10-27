
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

/// <summary>
/// Domain event raised when an order is placed.
/// </summary>
/// <param name="OrderId">Identifier of the placed order.</param>
internal sealed record OrderPlacedDomainEvent(int OrderId) : IDomainEvent;
