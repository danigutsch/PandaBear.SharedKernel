
using System.Text.RegularExpressions;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class ShouldAssertionExtensionsTests
{
    [Fact]
    public void ExactlyOne_returns_the_only_collection_item()
    {
        int[] values = [42];

        var value = values.ShouldHaveSingleItem();

        (value).ShouldBe(42);
    }

    [Fact]
    public void Should_assertion_extensions_support_migrated_xunit_assertion_overloads()
    {
        string[] values = ["alpha", "beta"];

        var matchingValue = values.ShouldHaveSingleItem(static value => value == "beta");
        values.ShouldNotContain("gamma", StringComparer.Ordinal);
        var assignableValue = ((object)"alpha").ShouldBeOfType<object>(exactMatch: false);

        matchingValue.ShouldBe("beta");
        assignableValue.ShouldBe("alpha");
    }

    [Fact]
    public void Should_assertion_extensions_cover_common_supported_shapes()
    {
        string[] values = ["alpha", "beta"];

        (1.234).ShouldBe(1.23, 2);
        ("alpha").ShouldNotBe("beta", StringComparer.OrdinalIgnoreCase);
        Action<string, string> contains = ShouldAssertionExtensions.ShouldContain;

        contains("alphabet", "alpha");
        values.ShouldContain(static value => value.StartsWith('b'));
        values.ShouldNotBeEmpty();
        ("booking-42").ShouldMatch(new Regex(@"^booking-\d+$", RegexOptions.None, TimeSpan.FromSeconds(1)));
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

}
