using SharedKernel.Testing;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>Tests what an error page tells the visitor for each failed status code.</summary>
[TestScope(TestScope.Unit)]
public sealed class ErrorPageKindsTests
{
    [Theory]
    [InlineData(404, ErrorPageKind.NotFound)]
    [InlineData(400, ErrorPageKind.RequestError)]
    [InlineData(403, ErrorPageKind.RequestError)]
    [InlineData(405, ErrorPageKind.RequestError)]
    [InlineData(410, ErrorPageKind.RequestError)]
    [InlineData(499, ErrorPageKind.RequestError)]
    [InlineData(408, ErrorPageKind.Failure)]
    [InlineData(429, ErrorPageKind.Failure)]
    [InlineData(500, ErrorPageKind.Failure)]
    [InlineData(503, ErrorPageKind.Failure)]
    [InlineData(599, ErrorPageKind.Failure)]
    public void A_failed_status_has_its_kind(int statusCode, ErrorPageKind kind)
    {
        // Act
        var actual = ErrorPageKinds.Of(statusCode);

        // Assert
        actual.ShouldBe(kind);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(399)]
    [InlineData(600)]
    public void A_status_that_is_not_an_error_is_refused(int statusCode)
    {
        // Act
        Action act = () => ErrorPageKinds.Of(statusCode);

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
