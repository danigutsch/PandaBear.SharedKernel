
using System.Text.RegularExpressions;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class TestAssertWrapperTests
{
    [Fact]
    public void ExactlyOne_returns_the_only_collection_item()
    {
        int[] values = [42];

        var value = values.ShouldHaveSingleItem();

        (value).ShouldBe(42);
    }

    [Fact]
    public void TestAssert_supports_migrated_xunit_assertion_overloads()
    {
        string[] values = ["alpha", "beta"];

        var matchingValue = TestAssert.ExactlyOne(values, static value => value == "beta");
        TestAssert.DoesNotContain("gamma", values, StringComparer.Ordinal);
        var assignableValue = TestAssert.IsType<object>("alpha", exactMatch: false);

        matchingValue.ShouldBe("beta");
        TestAssert.Same("alpha", assignableValue);
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
}
