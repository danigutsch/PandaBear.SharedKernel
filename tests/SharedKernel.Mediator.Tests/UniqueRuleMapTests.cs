namespace PandaBear.Mediator.Tests;

/// <summary>Tests the validation of <see cref="UniqueRuleMap"/>.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UniqueRuleMapTests
{
    [Fact]
    public void Add_returns_the_same_map_for_chaining()
    {
        // Arrange
        var map = new UniqueRuleMap();

        // Act
        var returned = map.Add("a", "email", "taken").Add("b", "code", "taken");

        // Assert
        returned.ShouldBeSameAs(map);
    }

    [Theory]
    [InlineData("", "f", "m")]
    [InlineData(" ", "f", "m")]
    [InlineData("r", " ", "m")]
    [InlineData("r", "f", "")]
    [InlineData("r", "f", "  ")]
    public void A_blank_argument_is_rejected(string rule, string field, string message)
    {
        // Arrange
        var map = new UniqueRuleMap();

        // Act
        Action act = () => map.Add(rule, field, message);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_null_argument_is_rejected(int position)
    {
        // Arrange
        var map = new UniqueRuleMap();

        // Act
        Action act = () => map.Add(position == 0 ? null! : "r", position == 1 ? null! : "f", position == 2 ? null! : "m");

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void A_duplicate_rule_is_rejected()
    {
        // Arrange
        var map = new UniqueRuleMap().Add("r", "email", "taken");

        // Act
        Action act = () => map.Add("r", "other", "other message");

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rules_are_compared_case_sensitively()
    {
        // Arrange
        var map = new UniqueRuleMap().Add("Rule", "email", "taken");

        // Act
        var returned = map.Add("rule", "email", "taken");

        // Assert
        returned.ShouldBeSameAs(map);
    }
}
