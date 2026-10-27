using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Playwright;
using PandaBear.Testing.Data;
using PandaBear.Testing.Http;
using PandaBear.Testing.Playwright;
using PandaBear.Testing.Roslyn;
using PandaBear.Testing.Snapshots;
using PandaBear.Testing.Web;
using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the shared testing helpers for unique IDs, HTTP responses, snapshots, bUnit, Playwright, Roslyn options, and the Should assertion wrappers.</summary>
public sealed class SharedKernelTestingHelperTests
{
    [Fact]
    public static void Test_unique_id_creates_prefixed_values()
    {
        // Act
        var value = TestUniqueId.Create("booking");

        // Assert
        value.ShouldStartWith("booking-", StringComparison.Ordinal);
        value.Length.ShouldBeGreaterThan("booking-".Length);
    }

    [Fact]
    public static void Test_unique_id_rejects_blank_prefixes()
    {
        // Arrange
        Action action = () => TestUniqueId.Create(" ");

        // Act, Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public static async Task Http_response_factory_creates_utf8_json_response()
    {
        // Act
        using var response = HttpResponseFactory.Json("{\"status\":\"ok\"}", HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        body.ShouldBe("{\"status\":\"ok\"}");
    }

    [Fact]
    public static void Http_response_factory_rejects_null_json()
    {
        // Arrange
        Action action = () => HttpResponseFactory.Json(null!);

        // Act, Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static void Snapshot_text_normalizes_line_endings_and_trailing_whitespace()
    {
        // Act
        var text = SnapshotText.Normalize("one  \r\ntwo\t\rthree  ");

        // Assert
        text.ShouldBe("one\ntwo\nthree");
    }

    [Fact]
    public static void Snapshot_text_rejects_null_text()
    {
        // Arrange
        Action action = () => SnapshotText.Normalize(null!);

        // Act, Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static void Json_snapshot_artifact_set_matches_semantically_equal_json()
    {
        // Arrange
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("catalog.openapi.json", "{\"openapi\":\"3.1.1\",\"info\":{\"title\":\"Catalog\"}}");
        directory.WriteGenerated("Api_catalog.json", "{\n  \"openapi\": \"3.1.1\",\n  \"info\": {\n    \"title\": \"Catalog\"\n  }\n}");
        var snapshots = directory.CreateSet();

        // Act
        snapshots.AssertGeneratedArtifactMatchesCanonicalSnapshot("catalog");

        // Assert
        true.ShouldBeTrue();
    }

    [Fact]
    public static void Json_snapshot_artifact_set_rejects_drift()
    {
        // Arrange
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("catalog.openapi.json", "{\"openapi\":\"3.1.1\"}");
        directory.WriteGenerated("Api_catalog.json", "{\"openapi\":\"3.0.0\"}");
        var snapshots = directory.CreateSet();
        Action action = () => snapshots.AssertCanonicalArtifactsMatchGeneratedArtifacts();

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("snapshot drift", StringComparison.Ordinal);
    }

    [Fact]
    public static void Json_snapshot_artifact_set_supports_generated_file_name_overrides()
    {
        // Arrange
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("v1.openapi.json", "{\"openapi\":\"3.1.1\",\"info\":{\"version\":\"1.0\"}}");
        directory.WriteGenerated("Api.json", "{\"info\":{\"version\":\"1.0\"},\"openapi\":\"3.1.1\"}");
        var snapshots = directory.CreateSet(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["v1"] = "Api.json"
        });

        // Act
        var drift = snapshots.GetArtifactDrift();

        // Assert
        drift.ShouldBeEmpty();
    }

    [Fact]
    public static void Bunit_element_assertions_accept_expected_class()
    {
        // Act
        BunitElementAssertions.HasClass(["card", "active"], "active");

        // Assert
        true.ShouldBeTrue();
    }

    [Fact]
    public static void Bunit_element_assertions_reject_missing_class()
    {
        // Arrange
        Action action = () => BunitElementAssertions.HasClass(["card"], "active");

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("active", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Playwright_title_assertion_rejects_null_pages()
    {
        // Arrange
        IPage page = null!;
        Func<Task> action = () => page.ShouldHaveTitle("Bookings");

        // Act
        var exception = await action.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("page");
    }

    [Fact]
    public void Analyzer_config_options_provider_returns_registered_values()
    {
        // Arrange
        var provider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty.Add("build_property.RootNamespace", "PandaBear.Testing.Tests"));

        // Act
        var options = provider.GlobalOptions;
        var exists = options.TryGetValue("build_property.RootNamespace", out var value);
        var syntaxTree = CSharpSyntaxTree.ParseText(string.Empty, cancellationToken: TestContext.Current.CancellationToken);
        var syntaxTreeOptions = provider.GetOptions(syntaxTree);

        // Assert
        exists.ShouldBeTrue();
        value.ShouldBe("PandaBear.Testing.Tests");
        syntaxTreeOptions.ShouldBeSameAs(TestAnalyzerConfigOptions.Empty);
    }

    [Fact]
    public void Exception_assertions_return_expected_inner_exception()
    {
        // Arrange
        Action invocation = () => throw new TargetInvocationException(new InvalidOperationException("expected"));

        // Act
        var exception = invocation.ShouldThrowInner<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("expected");
    }

    [Fact]
    public void Exception_assertions_reject_unexpected_inner_exception()
    {
        // Arrange
        Action invocation = () => throw new TargetInvocationException(new InvalidOperationException("expected"));
        Action action = () => invocation.ShouldThrowInner<ArgumentException>();
        var argumentExceptionName = typeof(ArgumentException).FullName;

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        argumentExceptionName.ShouldNotBeNull();
        exception.Message.ShouldContain(argumentExceptionName, StringComparison.Ordinal);
    }

    [Fact]
    public static void Exception_assertions_reject_missing_reflection_exception()
    {
        // Arrange
        Action invocation = () => { };
        Action action = () => invocation.ShouldThrowInner<InvalidOperationException>();
        var targetInvocationExceptionName = typeof(TargetInvocationException).FullName;

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        targetInvocationExceptionName.ShouldNotBeNull();
        exception.Message.ShouldContain(targetInvocationExceptionName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_assert_wraps_remaining_xunit_shapes()
    {
        // Arrange
        int[] singleItem = [1];
        int[] twoItems = [1, 2];
        int[] fortyTwo = [42];

        // Act, Assert
        true.ShouldBeTrue();
        var convertedValue = (bool?)true;
        convertedValue.ShouldBeTrue();
        false.ShouldBeFalse();
        var convertedValue2 = (bool?)false;
        convertedValue2.ShouldBeFalse();
        1.234.ShouldBe(1.23, 2);
        1.235.ShouldBe(1.23, 2, MidpointRounding.ToZero);
        "beta".ShouldNotBe("alpha");
        var convertedValue3 = (object?)null;
        convertedValue3.ShouldBeNull();
        var actualValue = this;
        actualValue.ShouldBeSameAs(this);
        Array.Empty<int>().ShouldBeEmpty();
        singleItem.ShouldNotBeEmpty();
        twoItems.ShouldHaveCount(2);
        singleItem.ShouldNotContain(2);
        singleItem.ShouldNotContain(value => value == 2);
        "alphabet".ShouldNotContain("beta", StringComparison.Ordinal);
        "alphabet".ShouldNotContain("BETA", StringComparison.Ordinal);
        twoItems.ShouldAllSatisfy(value => value.ShouldBeGreaterThan(0));
        twoItems.ShouldMatchCollection(value => value.ShouldBe(1), value => value.ShouldBe(2));
        "value".ShouldBeOfType<string>().ShouldBe("value");
        "value".ShouldNotBeOfType<int>();
        "value".ShouldBeAssignableTo<object>().ShouldBe("value");
        "value".ShouldNotBeAssignableTo<IDisposable>();
        5.ShouldBeInRange(1, 9);
        "alphabet".ShouldEndWith("bet", StringComparison.Ordinal);
        "alphabet".ShouldEndWith("BET", StringComparison.OrdinalIgnoreCase);
        "alphabet".ShouldMatch("^alpha");
        "alphabet".ShouldMatch(new Regex("bet$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        "alphabet".ShouldNotMatch(new Regex("gamma", RegexOptions.None, TimeSpan.FromSeconds(1)));
        ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Action)(() => throw new InvalidOperationException()));
        ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => throw new InvalidOperationException()));
        await ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<Task>)(() => Task.FromException(new InvalidOperationException())));
        await ShouldAssertionExtensions.ShouldThrowAssignableTo<ArgumentException>((Func<Task>)(() => Task.FromException(new ArgumentException("expected"))));
        "value".ShouldNotBeNull().ShouldBe("value");
        5.ShouldBe(5);
        fortyTwo.ShouldHaveSingleItem().ShouldBe(42);
        singleItem.ShouldContain(1, EqualityComparer<int>.Default);
        singleItem.ShouldContain(value => value == 1);
        "alphabet".ShouldContain("pha", StringComparison.Ordinal);
        "alphabet".ShouldStartWith("alpha", StringComparison.Ordinal);
        "alphabet".ShouldStartWith("ALPHA", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public static async Task Should_extensions_wrap_remaining_xunit_shapes()
    {
        // Act, Assert
        "alpha".ShouldBe("ALPHA", StringComparer.OrdinalIgnoreCase);
        "alpha".ShouldNotBe("beta", StringComparer.Ordinal);
        "value".ShouldBeOfType<string>().ShouldBe("value");
        "value".ShouldBeAssignableTo<object>().ShouldBe("value");
        "value".ShouldNotBeAssignableTo<IDisposable>();
        var convertedValue = (int?)5;
        convertedValue.ShouldNotBeNull().ShouldBe(5);
        false.ShouldBeFalse();
        var convertedValue2 = (bool?)false;
        convertedValue2.ShouldBeFalse();
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
        "alphabet".ShouldNotContain("GAMMA", StringComparison.OrdinalIgnoreCase);
        "alphabet".ShouldEndWith("BET", StringComparison.OrdinalIgnoreCase);
        "alphabet".ShouldMatch(new Regex("bet$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        var positiveValues = new[] { 1, 2 };
        positiveValues.ShouldAllSatisfy(value => value.ShouldBeGreaterThan(0));

        Func<Task> action = () => Task.FromException(new InvalidOperationException("expected"));

        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("expected");
    }

}
