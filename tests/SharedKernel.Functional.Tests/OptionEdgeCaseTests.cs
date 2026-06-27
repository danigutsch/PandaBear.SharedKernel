namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class OptionEdgeCaseTests
{
    [Fact]
    public void Rejects_Null_Values()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Option.Some(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Returns_A_Useful_String_For_Some()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var text = option.ToString();

        // Assert
        Assert.Equal("Some(porto)", text);
    }

    [Fact]
    public void Returns_A_Useful_String_For_None()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var text = option.ToString();

        // Assert
        Assert.Equal("None", text);
    }
}
