using System.Net;
using System.Net.Http.Headers;
using PandaBear.Testing.Http;
using Xunit.Sdk;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the header, no-store, and entity tag assertions.</summary>
public sealed class HttpHeaderAssertionTests
{
    [Theory]
    [InlineData("x-request-id")]
    [InlineData("X-REQUEST-ID")]
    [InlineData("X-Request-Id")]
    public void Header_name_is_matched_case_insensitively(string name)
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        response.Headers.Add("X-Request-Id", "abc");

        // Act
        var result = response.ShouldHaveHeader(name, "abc");

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public void Header_is_found_on_the_content_headers_too()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "{}");
        response.Content.Headers.ContentLanguage.Add("pt-BR");

        // Act
        var result = response.ShouldHaveHeader("content-language", "pt-BR");

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public void Header_with_several_values_matches_one_value_or_all_joined()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        response.Headers.Add("Vary", "Accept");
        response.Headers.Add("Vary", "Origin");
        Action other = () => response.ShouldHaveHeader("Vary", "Cookie");

        // Act
        response.ShouldHaveHeader("Vary", "Origin");
        response.ShouldHaveHeader("Vary", "Accept, Origin");
        var failure = other.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected header 'Vary' to be 'Cookie' but was 'Accept, Origin'.");
    }

    [Fact]
    public void Header_value_is_compared_case_sensitively()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        response.Headers.Add("X-Mode", "Fast");
        Action assertion = () => response.ShouldHaveHeader("X-Mode", "fast");

        // Act
        var failure = assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected header 'X-Mode' to be 'fast' but was 'Fast'.");
    }

    [Fact]
    public void Missing_header_fails()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        Action assertion = () => response.ShouldHaveHeader("X-Missing", "a");

        // Act
        var failure = assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected header 'X-Missing' to be 'a' but the response has none.");
    }

    [Theory]
    [InlineData("Set-Cookie", "session=secret")]
    [InlineData("authorization", "Bearer secret")]
    [InlineData("Proxy-Authenticate", "Basic realm=secret")]
    public void Credential_header_values_are_never_quoted(string name, string secret)
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation(name, secret);
        Action assertion = () => response.ShouldHaveHeader(name, "expected");

        // Act
        var failure = assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe($"Expected header '{name}' to be 'expected' but was '<redacted>'.");
    }

    [Fact]
    public void Header_assertion_validates_arguments()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        HttpResponseMessage missing = null!;
        Action nullResponse = () => missing.ShouldHaveHeader("X-A", "a");
        Action nullName = () => response.ShouldHaveHeader(null!, "a");
        Action nullValue = () => response.ShouldHaveHeader("X-A", null!);

        // Act, Assert
        nullResponse.ShouldThrow<ArgumentNullException>();
        nullName.ShouldThrow<ArgumentNullException>();
        nullValue.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void No_store_accepts_the_headers_written_by_the_http_caching_package()
    {
        // Arrange
        using var response = SuccessResponses.NoStore();

        // Act
        var result = response.ShouldBeNoStore();

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public void No_store_accepts_additional_cache_directives_and_an_older_expiry()
    {
        // Arrange
        using var response = SuccessResponses.NoStore();
        response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true, Private = true };
        response.Content.Headers.Expires = DateTimeOffset.UnixEpoch.AddDays(1);

        // Act
        var result = response.ShouldBeNoStore();

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public void No_store_fails_when_cache_control_lacks_no_store_or_is_missing()
    {
        // Arrange
        using var other = SuccessResponses.NoStore();
        other.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromSeconds(60) };
        using var missing = SuccessResponses.NoStore();
        missing.Headers.CacheControl = null;
        Action otherAssertion = () => other.ShouldBeNoStore();
        Action missingAssertion = () => missing.ShouldBeNoStore();

        // Act
        var otherFailure = otherAssertion.ShouldThrow<XunitException>();
        var missingFailure = missingAssertion.ShouldThrow<XunitException>();

        // Assert
        otherFailure.Message.ShouldBe("Expected header 'Cache-Control' to contain 'no-store' but was 'max-age=60'.");
        missingFailure.Message.ShouldBe("Expected header 'Cache-Control' to contain 'no-store' but the response has none.");
    }

    [Fact]
    public void No_store_fails_when_pragma_lacks_no_cache()
    {
        // Arrange
        using var response = SuccessResponses.NoStore();
        response.Headers.Pragma.Clear();
        Action assertion = () => response.ShouldBeNoStore();

        // Act
        var missingFailure = assertion.ShouldThrow<XunitException>();
        response.Headers.Pragma.Add(new NameValueHeaderValue("custom"));
        var otherFailure = assertion.ShouldThrow<XunitException>();

        // Assert
        missingFailure.Message.ShouldBe("Expected header 'Pragma' to contain 'no-cache' but the response has none.");
        otherFailure.Message.ShouldBe("Expected header 'Pragma' to contain 'no-cache' but was 'custom'.");
    }

    [Fact]
    public void No_store_fails_when_expires_is_missing_or_in_the_future()
    {
        // Arrange
        using var future = SuccessResponses.NoStore();
        future.Content.Headers.Expires = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var missing = SuccessResponses.NoStore();
        missing.Content.Headers.Expires = null;
        Action futureAssertion = () => future.ShouldBeNoStore();
        Action missingAssertion = () => missing.ShouldBeNoStore();

        // Act
        var futureFailure = futureAssertion.ShouldThrow<XunitException>();
        var missingFailure = missingAssertion.ShouldThrow<XunitException>();

        // Assert
        futureFailure.Message.ShouldBe("Expected header 'Expires' to be a date in the past but was 'Fri, 01 Jan 2100 00:00:00 GMT'.");
        missingFailure.Message.ShouldBe("Expected header 'Expires' to be a date in the past but the response has none.");
    }

    [Fact]
    public void No_store_rejects_a_null_response()
    {
        // Arrange
        HttpResponseMessage missing = null!;
        Action assertion = () => missing.ShouldBeNoStore();

        // Act, Assert
        assertion.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void ETag_accepts_the_expected_strong_tag()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");

        // Act
        var result = response.ShouldHaveETag("\"v1\"");

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public void ETag_fails_for_a_weak_tag_another_tag_or_no_tag()
    {
        // Arrange
        using var weak = SuccessResponses.Empty(HttpStatusCode.OK);
        weak.Headers.ETag = new EntityTagHeaderValue("\"v1\"", isWeak: true);
        using var other = SuccessResponses.Empty(HttpStatusCode.OK);
        other.Headers.ETag = new EntityTagHeaderValue("\"v2\"");
        using var missing = SuccessResponses.Empty(HttpStatusCode.OK);
        Action weakAssertion = () => weak.ShouldHaveETag("\"v1\"");
        Action otherAssertion = () => other.ShouldHaveETag("\"v1\"");
        Action missingAssertion = () => missing.ShouldHaveETag("\"v1\"");

        // Act
        var weakFailure = weakAssertion.ShouldThrow<XunitException>();
        var otherFailure = otherAssertion.ShouldThrow<XunitException>();
        var missingFailure = missingAssertion.ShouldThrow<XunitException>();

        // Assert
        weakFailure.Message.ShouldBe("Expected strong ETag '\"v1\"' but was 'W/\"v1\"'.");
        otherFailure.Message.ShouldBe("Expected strong ETag '\"v1\"' but was '\"v2\"'.");
        missingFailure.Message.ShouldBe("Expected strong ETag '\"v1\"' but the response has none.");
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("W/\"v1\"")]
    [InlineData("\"")]
    [InlineData("")]
    public void ETag_rejects_an_expectation_that_is_not_a_quoted_strong_tag(string expected)
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        Action assertion = () => response.ShouldHaveETag(expected);

        // Act, Assert
        assertion.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void ETag_assertion_validates_arguments()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        HttpResponseMessage missing = null!;
        Action nullResponse = () => missing.ShouldHaveETag("\"v1\"");
        Action nullTag = () => response.ShouldHaveETag(null!);

        // Act, Assert
        nullResponse.ShouldThrow<ArgumentNullException>();
        nullTag.ShouldThrow<ArgumentNullException>();
    }
}
