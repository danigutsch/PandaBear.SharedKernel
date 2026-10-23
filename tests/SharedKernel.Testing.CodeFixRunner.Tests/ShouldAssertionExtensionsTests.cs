
using System.Text.RegularExpressions;

namespace PandaBear.Testing.CodeFixRunner.Tests;

public sealed class ShouldAssertionExtensionsTests
{
    [Fact]
    public void ExactlyOne_returns_the_only_collection_item()
    {
        int[] values = [42];

        var value = values.ShouldHaveSingleItem();

        value.ShouldBe(42);
    }

    [Fact]
    public void Should_assertion_extensions_support_migrated_xunit_assertion_overloads()
    {
        string[] values = ["alpha", "beta"];
        object actualValue = "alpha";

        var matchingValue = values.ShouldHaveSingleItem(static value => value == "beta");
        values.ShouldNotContain("gamma", StringComparer.Ordinal);
        var assignableValue = actualValue.ShouldBeOfType<object>(exactMatch: false);

        matchingValue.ShouldBe("beta");
        assignableValue.ShouldBe("alpha");
    }

    [Fact]
    public void Should_be_one_of_matches_an_expected_value()
    {
        // Arrange
        const string Actual = "OPEN";
        string[] expected = ["OPEN", "CLOSED"];

        // Assert
        Actual.ShouldBeOneOf(expected);
    }

    [Fact]
    public void Should_assertion_extensions_cover_common_supported_shapes()
    {
        string[] values = ["alpha", "beta"];
        const double ActualValue = 1.234;
        const string ActualName = "alpha";
        const string BookingReference = "booking-42";

        ActualValue.ShouldBe(1.23, 2);
        ActualName.ShouldNotBe("beta", StringComparer.OrdinalIgnoreCase);
        Action<string, string> contains = ShouldAssertionExtensions.ShouldContain;

        contains("alphabet", "alpha");
        values.ShouldContain(static value => value.StartsWith('b'));
        values.ShouldNotBeEmpty();
        BookingReference.ShouldMatch(new Regex(@"^booking-\d+$", RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Should_contain_in_order_matches_distinct_repeated_fragments()
    {
        const string Actual = "first second second third";

        Actual.ShouldContainInOrder("first", "second", "second", "third");
    }

    [Fact]
    public void Should_contain_in_order_rejects_out_of_order_fragments()
    {
        Action action = () => "second first".ShouldContainInOrder("first", "second");

        action.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void Should_contain_in_order_rejects_null_actual()
    {
        string? actual = null;
        Action action = () => actual.ShouldContainInOrder();

        action.ShouldThrow<Xunit.Sdk.NotNullException>();
    }

    [Fact]
    public void Assertions_with_a_user_message_pass_when_they_hold()
    {
        // Arrange
        int[] values = [1, 2];

        // Act, Assert
        values.ShouldBe([1, 2], "Both values stay.");
        values.ShouldContain(static value => value == 2, "The second value stays.");
        values.ShouldNotContain(static value => value == 3, "No third value appears.");
    }

    [Fact]
    public void Should_be_with_a_user_message_states_it_before_the_failure()
    {
        // Arrange
        Action action = () => 1.ShouldBe(2, "Each item counts once.");

        // Act
        var exception = action.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        exception.Message.ShouldStartWith("Each item counts once.", StringComparison.Ordinal);
        exception.Message.ShouldContain(exception.InnerException.ShouldBeOfType<Xunit.Sdk.EqualException>().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_contain_with_a_user_message_states_it_before_the_failure()
    {
        // Arrange
        int[] values = [1, 2];
        Action action = () => values.ShouldContain(static value => value == 3, "The third value is added.");

        // Act
        var exception = action.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        exception.Message.ShouldStartWith("The third value is added.", StringComparison.Ordinal);
        exception.Message.ShouldContain(exception.InnerException.ShouldBeOfType<Xunit.Sdk.ContainsException>().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_not_contain_with_a_user_message_states_it_before_the_failure()
    {
        // Arrange
        int[] values = [1, 2];
        Action action = () => values.ShouldNotContain(static value => value == 2, "The second value is removed.");

        // Act
        var exception = action.ShouldThrow<Xunit.Sdk.XunitException>();

        // Assert
        exception.Message.ShouldStartWith("The second value is removed.", StringComparison.Ordinal);
        exception.Message.ShouldContain(exception.InnerException.ShouldBeOfType<Xunit.Sdk.DoesNotContainException>().Message, StringComparison.Ordinal);
    }
}
