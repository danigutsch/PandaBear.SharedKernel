namespace PandaBear.Domain.Tests;

/// <summary>Minimal domain event used by the dispatcher tests.</summary>
/// <param name="Name">The event name used to tell test events apart.</param>
internal sealed record TestDomainEvent(string Name) : IDomainEvent;
