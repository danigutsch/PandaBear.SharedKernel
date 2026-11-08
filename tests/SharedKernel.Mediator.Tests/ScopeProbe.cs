namespace PandaBear.Mediator.Tests;

/// <summary>Records which scopes were disposed.</summary>
internal sealed class ScopeProbe
{
    /// <summary>Gets the ids of the disposed scopes, in order.</summary>
    public List<Guid> DisposedScopes { get; } = [];
}
