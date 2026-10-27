namespace PandaBear.Documentation.Tests;

/// <summary>Trait values that tag the documentation tests by component, capability and category.</summary>
internal static class TestTraits
{
    /// <summary>Component trait value for the SharedKernel.Documentation library.</summary>
    public const string DocumentationComponent = "SharedKernel.Documentation";
    /// <summary>Capability trait value for diagram and table generation tests.</summary>
    public const string DocumentationGenerationCapability = "documentation-generation";
    /// <summary>Capability trait value for command-line tool tests.</summary>
    public const string CommandLineCapability = "command-line";
    /// <summary>Category trait value for tests of core generation and checking behavior.</summary>
    public const string CoreBehaviorCategory = "core-behavior";
    /// <summary>Category trait value for tests of the command-line surface.</summary>
    public const string CommandLineCategory = "command-line";
}
