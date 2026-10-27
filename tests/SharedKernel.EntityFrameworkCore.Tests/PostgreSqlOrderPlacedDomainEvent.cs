using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Domain event raised when a PostgreSQL test order is placed.
/// </summary>
/// <param name="OrderId">Identifier of the placed order.</param>
internal sealed record PostgreSqlOrderPlacedDomainEvent(int OrderId) : IDomainEvent;
