namespace PandaBear.EventSourcing.Tests;

/// <summary>A test domain event recording a new aggregate name.</summary>
/// <param name="Name">The new name.</param>
internal sealed record NameChanged(string Name);
