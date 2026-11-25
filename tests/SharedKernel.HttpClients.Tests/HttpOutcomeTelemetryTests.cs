using SharedKernel.Testing;

namespace SharedKernel.HttpClients.Tests;

/// <summary>Tests that the outcome span reports each <see cref="HttpOutcomeKind"/> as a lowercase snake_case attribute value.</summary>
[Trait(TestTraitNames.ScopeName, "unit")]
public sealed class HttpOutcomeTelemetryTests
{
    [Theory]
    [InlineData(HttpOutcomeKind.Succeeded, "succeeded")]
    [InlineData(HttpOutcomeKind.ValidationProblem, "validation_problem")]
    [InlineData(HttpOutcomeKind.NotFound, "not_found")]
    [InlineData(HttpOutcomeKind.Unauthorized, "unauthorized")]
    [InlineData(HttpOutcomeKind.Forbidden, "forbidden")]
    [InlineData(HttpOutcomeKind.Conflict, "conflict")]
    [InlineData(HttpOutcomeKind.EmptyBody, "empty_body")]
    [InlineData(HttpOutcomeKind.MalformedBody, "malformed_body")]
    [InlineData(HttpOutcomeKind.UnexpectedStatus, "unexpected_status")]
    [InlineData(HttpOutcomeKind.UnsupportedContentType, "unsupported_content_type")]
    [InlineData(HttpOutcomeKind.TimedOut, "timed_out")]
    [InlineData(HttpOutcomeKind.TransportFailure, "transport_failure")]
    public void Outcome_kinds_are_tagged_in_snake_case(HttpOutcomeKind kind, string expected)
    {
        // Act
        var value = HttpOutcomeMapper.OutcomeTagValue(kind);

        // Assert
        value.ShouldBe(expected);
    }

    [Fact]
    public void Activity_source_is_named_after_the_package()
    {
        // Act
        var name = HttpOutcomeMapper.ActivitySourceName;

        // Assert
        name.ShouldBe("SharedKernel.HttpClients");
        HttpOutcomeMapper.OutcomeActivitySource.Name.ShouldBe(name);
    }
}
