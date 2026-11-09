using Microsoft.AspNetCore.Http;

namespace PandaBear.Results.AspNet.Tests;

/// <summary>Tests how a record version travels as an entity tag.</summary>
public sealed class ConcurrencyHeadersTests
{
    [Theory]
    [InlineData(0L, "\"0\"")]
    [InlineData(42L, "\"42\"")]
    [InlineData(long.MaxValue, "\"9223372036854775807\"")]
    public void A_version_is_formatted_as_a_quoted_strong_entity_tag(long version, string expected)
    {
        // Act
        var tag = ConcurrencyHeaders.ETag(version);

        // Assert
        tag.ShouldBe(expected);
    }

    [Fact]
    public void A_negative_version_cannot_be_formatted()
    {
        // Act
        var exception = ShouldAssertionExtensions.ShouldThrow<ArgumentOutOfRangeException>((Func<object?>)(() => ConcurrencyHeaders.ETag(-1)));

        // Assert
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void A_strong_entity_tag_is_read_back_as_the_version()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch("\"7\"");

        // Act
        var version = ConcurrencyHeaders.ReadVersion(request);

        // Assert
        version.IsSuccess.ShouldBeTrue();
        version.Value.ShouldBe(7L);
    }

    [Fact]
    public void A_formatted_version_round_trips()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch(ConcurrencyHeaders.ETag(long.MaxValue));

        // Act
        var version = ConcurrencyHeaders.ReadVersion(request);

        // Assert
        version.Value.ShouldBe(long.MaxValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("W/\"7\"")]
    [InlineData("*")]
    [InlineData("\"7\", \"8\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"+1\"")]
    [InlineData("\" 1\"")]
    [InlineData("\"\"")]
    [InlineData("\"7\", junk")]
    [InlineData("7")]
    [InlineData("\"007\"")]
    [InlineData("\"9223372036854775808\"")]
    public void An_unusable_if_match_header_is_a_validation_failure_keyed_to_version(string? header)
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch(header);

        // Act
        var version = ConcurrencyHeaders.ReadVersion(request);

        // Assert
        version.IsFailure.ShouldBeTrue();
        version.Status.ShouldBe(ResultStatus.Invalid);
        version.WithoutValue().ValidationErrors().Keys.ShouldBe(["version"]);
    }

    [Fact]
    public void A_missing_request_is_rejected()
    {
        // Act
        var exception = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => ConcurrencyHeaders.ReadVersion(null!)));

        // Assert
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task An_update_sends_the_version_it_read_and_converts_the_outcome()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch("\"5\"");
        long? sent = null;

        // Act
        var http = await ConcurrencyHeaders.Update(request, version =>
        {
            sent = version;
            return ValueTask.FromResult(Result.Ok());
        });

        // Assert
        sent.ShouldBe(5L);
        http.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
    }

    [Fact]
    public async Task An_update_without_a_usable_version_does_not_run_and_answers_a_validation_problem()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch(null);
        var ran = false;

        // Act
        var http = await ConcurrencyHeaders.Update(request, _ =>
        {
            ran = true;
            return ValueTask.FromResult(Result.Ok());
        });

        // Assert
        ran.ShouldBeFalse();
        var problem = http.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task A_stale_update_answers_the_conflict_the_operation_reports()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch("\"5\"");

        // Act
        var http = await ConcurrencyHeaders.Update(request, _ => ValueTask.FromResult(Result.Conflict("stale")));

        // Assert
        var problem = http.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void An_update_requires_a_request_and_an_operation()
    {
        // Arrange
        var request = ConcurrencyTestSupport.RequestWithIfMatch("\"5\"");

        // Act
        var noRequest = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => ConcurrencyHeaders.Update(null!, _ => ValueTask.FromResult(Result.Ok()))));
        var noOperation = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => ConcurrencyHeaders.Update(request, null!)));

        // Assert
        noRequest.ParamName.ShouldBe("request");
        noOperation.ParamName.ShouldBe("send");
    }
}
