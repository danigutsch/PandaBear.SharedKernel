using SharedKernel.Testing.Data;
using SharedKernel.Testing.Http;
using SharedKernel.Testing.Roslyn;
using SharedKernel.Testing.Snapshots;
using SharedKernel.Testing.Web;
using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SharedKernel.Testing.Tests;

public sealed class SharedKernelTestingHelperTests
{
    [Fact]
    public static void Test_unique_id_creates_prefixed_values()
    {
        var value = TestUniqueId.Create("booking");

        value.ShouldStartWith("booking-");
        value.Length.ShouldBeGreaterThan("booking-".Length);
    }

    [Fact]
    public static void Test_unique_id_rejects_blank_prefixes()
    {
        Action action = () => TestUniqueId.Create(" ");

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public static async Task Http_response_factory_creates_utf8_json_response()
    {
        using var response = HttpResponseFactory.Json("{\"status\":\"ok\"}", HttpStatusCode.Accepted);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("{\"status\":\"ok\"}");
    }

    [Fact]
    public static void Http_response_factory_rejects_null_json()
    {
        Action action = () => HttpResponseFactory.Json(null!);

        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static void Snapshot_text_normalizes_line_endings_and_trailing_whitespace()
    {
        var text = SnapshotText.Normalize("one  \r\ntwo\t\rthree  ");

        text.ShouldBe("one\ntwo\nthree");
    }

    [Fact]
    public static void Snapshot_text_rejects_null_text()
    {
        Action action = () => SnapshotText.Normalize(null!);

        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static void Bunit_element_assertions_accept_expected_class()
    {
        BunitElementAssertions.HasClass(["card", "active"], "active");

        true.ShouldBeTrue();
    }

    [Fact]
    public static void Bunit_element_assertions_reject_missing_class()
    {
        Action action = () => BunitElementAssertions.HasClass(["card"], "active");

        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain("active", StringComparison.Ordinal);
    }

    [Fact]
    public void Analyzer_config_options_provider_returns_registered_values()
    {
        var provider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty.Add("build_property.RootNamespace", "SharedKernel.Testing.Tests"));

        var options = provider.GlobalOptions;
        var exists = options.TryGetValue("build_property.RootNamespace", out var value);

        exists.ShouldBeTrue();
        value.ShouldBe("SharedKernel.Testing.Tests");
        provider.GetOptions(tree: null!).ShouldBeSameAs(TestAnalyzerConfigOptions.Empty);
        provider.GetOptions(textFile: null!).ShouldBeSameAs(TestAnalyzerConfigOptions.Empty);
    }

    [Fact]
    public void Exception_assertions_return_expected_inner_exception()
    {
        var exception = ExceptionAssertions.ThrowsInner<InvalidOperationException>(
            () => throw new TargetInvocationException(new InvalidOperationException("expected")));

        exception.Message.ShouldBe("expected");
    }

    [Fact]
    public void Exception_assertions_reject_unexpected_inner_exception()
    {
        Action action = () => ExceptionAssertions.ThrowsInner<ArgumentException>(
            () => throw new TargetInvocationException(new InvalidOperationException("expected")));

        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(typeof(ArgumentException).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public static void Exception_assertions_reject_missing_reflection_exception()
    {
        Action action = () => ExceptionAssertions.ThrowsInner<InvalidOperationException>(() => { });

        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(typeof(TargetInvocationException).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_assert_wraps_remaining_xunit_shapes()
    {
        TestAssert.True(true);
        TestAssert.True((bool?)true);
        TestAssert.False(false);
        TestAssert.False((bool?)false);
        TestAssert.Equal(1.23, 1.234, 2);
        TestAssert.Equal(1.23, 1.235, 2, MidpointRounding.ToZero);
        TestAssert.NotEqual("alpha", "beta");
        TestAssert.Null(null);
        TestAssert.Same(this, this);
        TestAssert.Empty(Array.Empty<int>());
        TestAssert.NotEmpty([1]);
        TestAssert.DoesNotContain(2, [1]);
        TestAssert.DoesNotContain([1], value => value == 2);
        TestAssert.DoesNotContain("beta", "alphabet", StringComparison.Ordinal);
        TestAssert.DoesNotContain("BETA", "alphabet", StringComparison.Ordinal);
        TestAssert.All([1, 2], value => value.ShouldBeGreaterThan(0));
        TestAssert.Collection([1, 2], value => value.ShouldBe(1), value => value.ShouldBe(2));
        TestAssert.IsType<string>("value").ShouldBe("value");
        TestAssert.IsNotType<int>("value");
        TestAssert.IsAssignableFrom<object>("value").ShouldBe("value");
        TestAssert.IsNotAssignableFrom<IDisposable>("value");
        TestAssert.InRange(5, 1, 9);
        TestAssert.EndsWith("bet", "alphabet", StringComparison.Ordinal);
        TestAssert.EndsWith("BET", "alphabet", StringComparison.OrdinalIgnoreCase);
        TestAssert.Matches("^alpha", "alphabet");
        TestAssert.Matches(new Regex("bet$", RegexOptions.None, TimeSpan.FromSeconds(1)), "alphabet");
        TestAssert.DoesNotMatch(new Regex("gamma", RegexOptions.None, TimeSpan.FromSeconds(1)), "alphabet");
        TestAssert.Throws<InvalidOperationException>((Action)(() => throw new InvalidOperationException()));
        TestAssert.Throws<InvalidOperationException>((Func<object?>)(() => throw new InvalidOperationException()));
        await TestAssert.Throws<InvalidOperationException>(() => Task.FromException(new InvalidOperationException()));
        await TestAssert.ThrowsAny<ArgumentException>(() => Task.FromException(new ArgumentException("expected")));
        TestAssert.NotNull("value").ShouldBe("value");
        TestAssert.NotNull<int>(5).ShouldBe(5);
        TestAssert.ExactlyOne([42]).ShouldBe(42);
        TestAssert.Contains(1, [1], EqualityComparer<int>.Default);
        TestAssert.Contains([1], value => value == 1);
        TestAssert.Contains("pha", "alphabet", StringComparison.Ordinal);
        TestAssert.StartsWith("alpha", "alphabet", StringComparison.Ordinal);
        TestAssert.StartsWith("ALPHA", "alphabet", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public static async Task Should_extensions_wrap_remaining_xunit_shapes()
    {
        "alpha".ShouldBe("ALPHA", StringComparer.OrdinalIgnoreCase);
        "alpha".ShouldNotBe("beta", StringComparer.Ordinal);
        "value".ShouldBeOfType<string>().ShouldBe("value");
        "value".ShouldBeAssignableTo<object>().ShouldBe("value");
        "value".ShouldNotBeAssignableTo<IDisposable>();
        ((int?)5).ShouldNotBeNull().ShouldBe(5);
        false.ShouldBeFalse();
        ((bool?)false).ShouldBeFalse();
        var singleValue = new[] { 1 };
        singleValue.ShouldContain(1, EqualityComparer<int>.Default);
        singleValue.ShouldContain(value => value == 1);
        var answer = new[] { 42 };
        answer.ShouldHaveSingleItem().ShouldBe(42);
        var values = new[] { 1, 42 };
        values.ShouldHaveSingleItem(value => value == 42).ShouldBe(42);
        5.ShouldBeInRange(1, 9);
        5.ShouldBeGreaterThanOrEqualTo(5);
        5.ShouldBeLessThan(6);
        5.ShouldBeLessThanOrEqualTo(5);
        "alphabet".ShouldEndWith("BET", StringComparison.OrdinalIgnoreCase);
        "alphabet".ShouldMatch(new Regex("bet$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        var positiveValues = new[] { 1, 2 };
        positiveValues.ShouldAllSatisfy(value => value.ShouldBeGreaterThan(0));

        Func<Task> action = () => Task.FromException(new InvalidOperationException("expected"));

        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("expected");
    }
}
