namespace PandaBear.Testing;

/// <summary>
/// The test seam a test exercises. The value of the <c>Scope</c> trait.
/// </summary>
public enum TestScope
{
    /// <summary>A single unit in isolation.</summary>
    Unit,

    /// <summary>Observable behavior described in business terms.</summary>
    Behavior,

    /// <summary>A component with its collaborators wired together in process.</summary>
    Component,

    /// <summary>A published contract such as an API schema or message shape.</summary>
    Contract,

    /// <summary>Web UI rendered against test doubles for the back end.</summary>
    UiIntegration,

    /// <summary>Real infrastructure such as a database or a hosted service.</summary>
    Integration,

    /// <summary>The whole system running end to end.</summary>
    System,

    /// <summary>Structural rules of the code base.</summary>
    Architecture,
}
