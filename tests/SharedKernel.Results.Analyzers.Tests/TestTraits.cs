namespace PandaBear.Results.Analyzers.Tests;

/// <summary>Trait names and values shared by the Results analyzer tests.</summary>
internal static class TestTraits
{
    /// <summary>Component trait value that identifies the SharedKernel.Results.Analyzers assembly.</summary>
    public const string ResultsAnalyzersComponent = "SharedKernel.Results.Analyzers";

    /// <summary>Capability trait value for the raw-result endpoint rule.</summary>
    public const string EndpointResultCapability = "EndpointResult";

    /// <summary>Capability trait value for the discarded-result rule.</summary>
    public const string DiscardedResultCapability = "DiscardedResult";
}
