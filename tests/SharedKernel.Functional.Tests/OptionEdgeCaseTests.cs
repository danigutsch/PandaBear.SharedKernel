namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class OptionEdgeCaseTests
{
    [Fact]
    public void Rejects_null_values()
    {
        // Act
        var exception = ((Func<object?>)(() => Option.Some(NullArgumentData.String()))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Returns_a_useful_string_for_some()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var text = option.ToString();

        // Assert
        text.ShouldBe("Some(porto)");
    }

    [Fact]
    public void Returns_a_useful_string_for_none()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var text = option.ToString();

        // Assert
        text.ShouldBe("None");
    }
}
