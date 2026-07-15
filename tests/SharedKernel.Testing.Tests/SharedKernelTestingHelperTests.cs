using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Playwright;
using SharedKernel.Testing.Data;
using SharedKernel.Testing.Http;
using SharedKernel.Testing.Playwright;
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

        value.ShouldStartWith("booking-", StringComparison.Ordinal);
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
    public static void Json_snapshot_artifact_set_matches_semantically_equal_json()
    {
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("catalog.openapi.json", "{\"openapi\":\"3.1.1\",\"info\":{\"title\":\"Catalog\"}}");
        directory.WriteGenerated("Api_catalog.json", "{\n  \"openapi\": \"3.1.1\",\n  \"info\": {\n    \"title\": \"Catalog\"\n  }\n}");
        var snapshots = directory.CreateSet();

        snapshots.AssertGeneratedArtifactMatchesCanonicalSnapshot("catalog");

        true.ShouldBeTrue();
    }

    [Fact]
    public static void Json_snapshot_artifact_set_rejects_drift()
    {
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("catalog.openapi.json", "{\"openapi\":\"3.1.1\"}");
        directory.WriteGenerated("Api_catalog.json", "{\"openapi\":\"3.0.0\"}");
        var snapshots = directory.CreateSet();

        Action action = () => snapshots.AssertCanonicalArtifactsMatchGeneratedArtifacts();

        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain("snapshot drift", StringComparison.Ordinal);
    }

    [Fact]
    public static void Json_snapshot_artifact_set_supports_generated_file_name_overrides()
    {
        using var directory = TemporarySnapshotDirectory.Create();
        directory.WriteCanonical("v1.openapi.json", "{\"openapi\":\"3.1.1\",\"info\":{\"version\":\"1.0\"}}");
        directory.WriteGenerated("Api.json", "{\"info\":{\"version\":\"1.0\"},\"openapi\":\"3.1.1\"}");
        var snapshots = directory.CreateSet(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["v1"] = "Api.json"
        });

        var drift = snapshots.GetArtifactDrift();

        drift.ShouldBeEmpty();
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
        var provider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty.Add("build_property.RootNamespace", "SharedKernel.Testing.Tests"));

        var options = provider.GlobalOptions;
        var exists = options.TryGetValue("build_property.RootNamespace", out var value);

        exists.ShouldBeTrue();
        value.ShouldBe("SharedKernel.Testing.Tests");
        var syntaxTree = CSharpSyntaxTree.ParseText(string.Empty, cancellationToken: TestContext.Current.CancellationToken);
        provider.GetOptions(syntaxTree).ShouldBeSameAs(TestAnalyzerConfigOptions.Empty);
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

        var argumentExceptionName = typeof(ArgumentException).FullName;
        argumentExceptionName.ShouldNotBeNull();
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(argumentExceptionName, StringComparison.Ordinal);
    }

    [Fact]
    public static void Exception_assertions_reject_missing_reflection_exception()
    {
        Action action = () => ExceptionAssertions.ThrowsInner<InvalidOperationException>(() => { });

        var targetInvocationExceptionName = typeof(TargetInvocationException).FullName;
        targetInvocationExceptionName.ShouldNotBeNull();
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(targetInvocationExceptionName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_assert_wraps_remaining_xunit_shapes()
    {
        int[] singleItem = [1];
        int[] twoItems = [1, 2];
        int[] fortyTwo = [42];

        (true).ShouldBeTrue();
        ((bool?)true).ShouldBeTrue();
        (false).ShouldBeFalse();
        ((bool?)false).ShouldBeFalse();
        (1.234).ShouldBe(1.23, 2);
        (1.235).ShouldBe(1.23, 2, MidpointRounding.ToZero);
        ("beta").ShouldNotBe("alpha");
        ((object?)null).ShouldBeNull();
        (this).ShouldBeSameAs(this);
        (Array.Empty<int>()).ShouldBeEmpty();
        (singleItem).ShouldNotBeEmpty();
        (twoItems).ShouldHaveCount(2);
        (singleItem).ShouldNotContain(2);
        (singleItem).ShouldNotContain(value => value == 2);
        ("alphabet").ShouldNotContain("beta", StringComparison.Ordinal);
        ("alphabet").ShouldNotContain("BETA", StringComparison.Ordinal);
        (twoItems).ShouldAllSatisfy(value => value.ShouldBeGreaterThan(0));
        (twoItems).ShouldMatchCollection(value => value.ShouldBe(1), value => value.ShouldBe(2));
        ("value").ShouldBeOfType<string>().ShouldBe("value");
        ("value").ShouldNotBeOfType<int>();
        ("value").ShouldBeAssignableTo<object>().ShouldBe("value");
        ("value").ShouldNotBeAssignableTo<IDisposable>();
        (5).ShouldBeInRange(1, 9);
        ("alphabet").ShouldEndWith("bet", StringComparison.Ordinal);
        ("alphabet").ShouldEndWith("BET", StringComparison.OrdinalIgnoreCase);
        ("alphabet").ShouldMatch("^alpha");
        ("alphabet").ShouldMatch(new Regex("bet$", RegexOptions.None, TimeSpan.FromSeconds(1)));
        ("alphabet").ShouldNotMatch(new Regex("gamma", RegexOptions.None, TimeSpan.FromSeconds(1)));
        ((Action)(() => throw new InvalidOperationException())).ShouldThrow<InvalidOperationException>();
        ((Func<object?>)(() => throw new InvalidOperationException())).ShouldThrow<InvalidOperationException>();
        await ((Func<Task>)(() => Task.FromException(new InvalidOperationException()))).ShouldThrow<InvalidOperationException>();
        await ((Func<Task>)(() => Task.FromException(new ArgumentException("expected")))).ShouldThrowAssignableTo<ArgumentException>();
        ("value").ShouldNotBeNull().ShouldBe("value");
        (5).ShouldBe(5);
        (fortyTwo).ShouldHaveSingleItem().ShouldBe(42);
        (singleItem).ShouldContain(1, EqualityComparer<int>.Default);
        (singleItem).ShouldContain(value => value == 1);
        ("alphabet").ShouldContain("pha", StringComparison.Ordinal);
        ("alphabet").ShouldStartWith("alpha", StringComparison.Ordinal);
        ("alphabet").ShouldStartWith("ALPHA", StringComparison.OrdinalIgnoreCase);
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
