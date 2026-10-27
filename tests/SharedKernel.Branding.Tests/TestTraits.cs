namespace PandaBear.Branding.Tests;

/// <summary>
/// Trait values that tag the branding tests by component and capability.
/// </summary>
internal static class TestTraits
{
    /// <summary>
    /// Component trait value naming the branding package.
    /// </summary>
    public const string BrandingComponent = "SharedKernel.Branding";
    /// <summary>
    /// Capability trait value for the branding validation tests.
    /// </summary>
    public const string ValidationCapability = "BrandingValidation";
    /// <summary>
    /// Capability trait value for the branding API client tests.
    /// </summary>
    public const string ApiClientCapability = "BrandingApiClient";
}
