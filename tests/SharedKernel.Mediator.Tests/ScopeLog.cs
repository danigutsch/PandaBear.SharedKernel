namespace PandaBear.Mediator.Tests;

/// <summary>Records what happened in a scope, in order.</summary>
internal sealed class ScopeLog
{
    /// <summary>Gets the recorded entries, in order.</summary>
    public List<string> Entries { get; } = [];
}
