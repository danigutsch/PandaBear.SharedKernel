namespace SharedKernel.Domain.Tests;

internal sealed record TestDomainEvent(string Name) : IDomainEvent;
