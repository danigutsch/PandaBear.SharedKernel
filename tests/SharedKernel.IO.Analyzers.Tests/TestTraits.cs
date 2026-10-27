namespace PandaBear.IO.Analyzers.Tests;

/// <summary>
/// Trait values that tag the IO analyzer tests by component and capability.
/// </summary>
internal static class TestTraits
{
    /// <summary>
    /// Component trait value naming the IO analyzers package.
    /// </summary>
    public const string IOAnalyzersComponent = "SharedKernel.IO.Analyzers";
    /// <summary>
    /// Capability trait value for the IO stream buffering checks.
    /// </summary>
    public const string StreamBufferingCapability = "StreamBuffering";
}
