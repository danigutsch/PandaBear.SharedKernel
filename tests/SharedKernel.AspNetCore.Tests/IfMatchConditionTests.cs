using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Tests the RFC 9110 <c>If-Match</c> condition: strict parsing of <c>*</c> or a list of entity tags, strong comparison against the current
/// tag, and the 428/400 problems for a missing or unusable header.
/// </summary>
public sealed class IfMatchConditionTests
{
    [Theory]
    [InlineData("\"a\"", "\"a\"", true)]
    [InlineData("\"a\", \"b\"", "\"b\"", true)]
    [InlineData("\"a\"", "\"b\"", false)]
    [InlineData("W/\"a\"", "\"a\"", false)]
    [InlineData("\"a\"", "W/\"a\"", false)]
    [InlineData("*", "\"anything\"", true)]
    public void A_condition_matches_the_current_tag_by_strong_comparison(string header, string current, bool expected)
    {
        // Arrange
        IfMatchCondition.TryParse(header, out var condition).ShouldBeTrue();

        // Act
        var matches = condition!.Matches(EntityTagHeaderValue.Parse(current));

        // Assert
        matches.ShouldBe(expected);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"a\"")]
    public void No_condition_matches_a_resource_that_does_not_exist(string header)
    {
        // Arrange
        IfMatchCondition.TryParse(header, out var condition).ShouldBeTrue();

        // Act
        var matches = condition!.Matches((EntityTagHeaderValue?)null);

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public void The_current_tag_can_be_given_as_its_quoted_text()
    {
        // Arrange
        IfMatchCondition.TryParse("\"7-12\"", out var condition).ShouldBeTrue();

        // Act
        var same = condition!.Matches("\"7-12\"");
        var other = condition.Matches("\"7-13\"");

        // Assert
        same.ShouldBeTrue();
        other.ShouldBeFalse();
    }

    [Fact]
    public void A_parsed_condition_exposes_the_wildcard_or_its_tags()
    {
        // Act
        IfMatchCondition.TryParse("*", out var any).ShouldBeTrue();
        IfMatchCondition.TryParse(new StringValues(["\"a\"", "\"b\""]), out var list).ShouldBeTrue();

        // Assert
        any!.MatchesAny.ShouldBeTrue();
        any.EntityTags.ShouldBeEmpty();
        list!.MatchesAny.ShouldBeFalse();
        list.EntityTags.Select(tag => tag.Tag.Value).ShouldBe(["\"a\"", "\"b\""]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("\"a\", b")]
    [InlineData("*, \"a\"")]
    public void An_unusable_header_value_is_not_a_condition(string header)
    {
        // Act
        var parsed = IfMatchCondition.TryParse(header, out var condition);

        // Assert
        parsed.ShouldBeFalse();
        condition.ShouldBeNull();
    }

    [Fact]
    public void Reading_a_request_without_the_header_answers_428()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var problem = IfMatchCondition.Read(context.Request, out var condition);

        // Assert
        condition.ShouldBeNull();
        var result = problem.ShouldNotBeNull();
        result.StatusCode.ShouldBe(StatusCodes.Status428PreconditionRequired);
        result.ProblemDetails.Status.ShouldBe(StatusCodes.Status428PreconditionRequired);
        result.ProblemDetails.Detail.ShouldBe(PreconditionProblems.RequiredDetail);
    }

    [Fact]
    public void Reading_a_request_with_an_unusable_header_answers_400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "not-a-tag";

        // Act
        var problem = IfMatchCondition.Read(context.Request, out var condition);

        // Assert
        condition.ShouldBeNull();
        var result = problem.ShouldNotBeNull();
        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.ProblemDetails.Detail.ShouldBe(PreconditionProblems.InvalidIfMatchDetail);
    }

    [Fact]
    public void Reading_a_request_with_a_usable_header_returns_the_condition_and_no_problem()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"3\"";

        // Act
        var problem = IfMatchCondition.Read(context.Request, out var condition);

        // Assert
        problem.ShouldBeNull();
        condition.ShouldNotBeNull().Matches("\"3\"").ShouldBeTrue();
    }

    [Fact]
    public void The_problems_carry_their_status_the_detail_and_the_extensions()
    {
        // Arrange
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "settings.stale" };

        // Act
        ProblemHttpResult required = PreconditionProblems.Required("Send If-Match.", extensions);
        ProblemHttpResult failed = PreconditionProblems.Failed("Reload and retry.", extensions);
        ProblemHttpResult invalid = PreconditionProblems.InvalidIfMatch("Bad If-Match.", extensions);

        // Assert
        (required.StatusCode, required.ProblemDetails.Detail).ShouldBe((StatusCodes.Status428PreconditionRequired, "Send If-Match."));
        (failed.StatusCode, failed.ProblemDetails.Detail).ShouldBe((StatusCodes.Status412PreconditionFailed, "Reload and retry."));
        (invalid.StatusCode, invalid.ProblemDetails.Detail).ShouldBe((StatusCodes.Status400BadRequest, "Bad If-Match."));
        failed.ProblemDetails.Extensions["code"].ShouldBe("settings.stale");
    }

    [Fact]
    public void A_failed_problem_without_a_detail_uses_the_default_detail()
    {
        // Act
        var failed = PreconditionProblems.Failed();

        // Assert
        failed.ProblemDetails.Detail.ShouldBe(PreconditionProblems.FailedDetail);
    }
}
