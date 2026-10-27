namespace PandaBear.Aspire.Analyzers.Tests;

/// <summary>
/// Trait values that tag the Aspire analyzer tests by component and capability.
/// </summary>
internal static class TestTraits
{
    /// <summary>
    /// Component trait value naming the Aspire analyzers package.
    /// </summary>
    public const string AspireAnalyzersComponent = "SharedKernel.Aspire.Analyzers";
    /// <summary>
    /// Capability trait value for the Aspire image pinning checks.
    /// </summary>
    public const string AspireImagePinningCapability = "AspireImagePinning";
}
