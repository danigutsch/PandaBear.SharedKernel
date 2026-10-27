using System.Diagnostics;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the text screening used before text reaches a user-facing surface.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class BoundedTextTests
{
    [Fact]
    public void Megabyte_of_regex_hostile_text_is_rejected_quickly_without_throwing()
    {
        // Arrange
        var hostile = string.Concat(Enumerable.Repeat("a.", 500_000));
        var clock = Stopwatch.StartNew();

        // Act
        var message = BoundedText.ToSingleLineMessage(hostile, 300);

        // Assert
        message.ShouldBeNull();
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("Failed at Contoso.Orders.Save(Order order)")]
    [InlineData("in Orders.cs:line 42")]
    [InlineData("A NullReferenceException occurred")]
    public void Each_stack_trace_pattern_is_rejected(string text)
    {
        // Arrange & Act
        var message = BoundedText.ToSingleLineMessage(text, 300);

        // Assert
        message.ShouldBeNull();
    }

    [Theory]
    [InlineData("Look at Foo(bar) before saving.")]
    [InlineData("The exception list is empty.")]
    [InlineData("Name must be at least 3 characters.")]
    public void Benign_messages_survive(string text)
    {
        // Arrange & Act
        var message = BoundedText.ToSingleLineMessage(text, 300);

        // Assert
        message.ShouldBe(text);
    }

    [Fact]
    public void Text_longer_than_four_times_the_bound_is_rejected_before_screening()
    {
        // Arrange & Act
        var message = BoundedText.ToSingleLineMessage(HostileText.VeryLong(1_201), 300);

        // Assert
        message.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Truncation_never_exceeds_the_bound(int bound)
    {
        // Arrange & Act
        var truncated = BoundedText.Truncate("abcdefghij", bound);

        // Assert
        truncated.Length.ShouldBeLessThanOrEqualTo(bound);
    }

    [Fact]
    public void Format_characters_are_removed_from_display_text_and_messages()
    {
        // Arrange & Act
        var display = BoundedText.ToDisplay("a\u202Eb\u2066c\u200Bd", 50);
        var message = BoundedText.ToSingleLineMessage("a\u202Eb\u200Bc", 50);

        // Assert
        display.ShouldBe("abcd");
        message.ShouldBe("abc");
    }
}
