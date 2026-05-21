namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.OptionCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
[Trait(TestTraits.TheoryName, TestTraits.MonadLawsTheory)]
public sealed class OptionMonadLawTests
{
    [Fact]
    public void Satisfies_The_Monad_Left_Identity_Law()
    {
        // Arrange
        const string value = "porto";

        // Act
        var left = Option.Some(value).Bind(static city => Option.Some(city.ToUpperInvariant()));
        var right = Option.Some(value.ToUpperInvariant());

        // Assert
        Assert.Equal(right, left);
    }

    [Fact]
    public void Satisfies_The_Monad_Right_Identity_Law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = option.Bind(Option.Some);

        // Assert
        Assert.Equal(option, bound);
    }

    [Fact]
    public void Satisfies_The_Monad_Associativity_Law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var left = option
            .Bind(static city => Option.Some(city.Trim()))
            .Bind(static city => Option.Some(city.ToUpperInvariant()));
        var right = option.Bind(static city =>
            Option.Some(city.Trim()).Bind(static trimmed => Option.Some(trimmed.ToUpperInvariant())));

        // Assert
        Assert.Equal(left, right);
    }
}
