namespace PandaBear.Domain.GeneratorTests;

/// <summary>Trait values shared by the domain source generator tests.</summary>
internal static class TestTraits
{
    /// <summary>Component trait value that identifies the SharedKernel.Domain.SourceGenerator assembly.</summary>
    public const string SourceGeneratorComponent = "SharedKernel.Domain.SourceGenerator";

    /// <summary>Capability trait value for strongly typed identity generation.</summary>
    public const string IdentityCapability = "Identity";

    /// <summary>Capability trait value for value object generation.</summary>
    public const string ValueObjectCapability = "ValueObject";
}
