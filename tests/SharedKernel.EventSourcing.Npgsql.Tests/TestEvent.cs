namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>A minimal event carrying only a name, used as the payload in event store tests.</summary>
/// <param name="Name">The name carried by the event.</param>
internal sealed record TestEvent(string Name);
