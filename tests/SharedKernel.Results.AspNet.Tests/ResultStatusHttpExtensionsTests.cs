using Microsoft.AspNetCore.Http;

namespace SharedKernel.Results.AspNet.Tests;

public sealed class ResultStatusHttpExtensionsTests
{
    [Theory]
    [InlineData(ResultStatus.Ok, StatusCodes.Status200OK)]
    [InlineData(ResultStatus.Created, StatusCodes.Status201Created)]
    [InlineData(ResultStatus.Accepted, StatusCodes.Status202Accepted)]
    [InlineData(ResultStatus.NoContent, StatusCodes.Status204NoContent)]
    [InlineData(ResultStatus.Invalid, StatusCodes.Status400BadRequest)]
    [InlineData(ResultStatus.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError)]
    [InlineData(ResultStatus.CriticalError, StatusCodes.Status500InternalServerError)]
    [InlineData(ResultStatus.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    public void ToHttpStatusCode_Returns_Expected_Code_For_Defined_Statuses(ResultStatus status, int expectedStatusCode)
    {
        // Act
        var actual = status.ToHttpStatusCode();

        // Assert
        Assert.Equal(expectedStatusCode, actual);
    }

    [Fact]
    public void ToHttpStatusCode_Rejects_Unknown_Statuses()
    {
        // Arrange
        var unknownStatus = Enum.Parse<ResultStatus>("2147483647");

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => unknownStatus.ToHttpStatusCode());

        // Assert
        Assert.Equal("status", exception.ParamName);
    }
}
