namespace SharedKernel.Testing.Mediator;

/// <summary>
/// Marks the test-only reference-dispatcher assembly.
/// </summary>
internal static class ReferenceDispatcherAssemblyMarker
{
    /// <summary>
    /// Gets the reference-dispatcher assembly.
    /// </summary>
    internal static System.Reflection.Assembly Assembly => typeof(ReferenceDispatcherAssemblyMarker).Assembly;
}
