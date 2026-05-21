namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.OptionCapability)]
[Trait(TestTraits.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class OptionEdgeCaseTests
{
    [Fact]
    public void Rejects_Null_Values()
    {
        // Act
        var exception = InvocationTestHelpers.InvokeStaticGenericAndCapture<ArgumentNullException>(
            typeof(Option),
            nameof(Option.Some),
            [typeof(string)],
            [typeof(string)],
            [null]);

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
