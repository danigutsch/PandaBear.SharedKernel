using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed record PostgreSqlOrderPlacedDomainEvent(int OrderId) : IDomainEvent;
