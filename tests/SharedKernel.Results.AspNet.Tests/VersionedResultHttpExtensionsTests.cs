using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Results.AspNet.Tests;

/// <summary>Tests the responses that carry a record version or a created location.</summary>
public sealed class VersionedResultHttpExtensionsTests
{
    [Fact]
    public async Task A_read_answers_ok_with_the_version_as_entity_tag()
    {
        // Arrange
        var result = Result.Ok(new ConcurrencyTestSupport.Row("a", 3));
        var context = ConcurrencyTestSupport.NewContext();

        // Act
        var http = result.ToHttpResult(row => row.Version);
        await http.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.Headers[HeaderNames.ETag].ToString().ShouldBe("\"3\"");
    }

    [Fact]
    public async Task A_failed_read_sends_no_entity_tag()
    {
        // Arrange
        var result = Result.NotFound<ConcurrencyTestSupport.Row>("missing");
        var context = ConcurrencyTestSupport.NewContext();

        // Act
        var http = result.ToHttpResult(row => row.Version);
        await http.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.Headers.ContainsKey(HeaderNames.ETag).ShouldBeFalse();
    }

    [Fact]
    public void A_read_requires_a_version_selector()
    {
        // Act
        var exception = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => Result.Ok(new ConcurrencyTestSupport.Row("a", 1)).ToHttpResult(null!)));

        // Assert
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void A_successful_creation_is_created_with_a_location_and_the_chosen_body()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var http = Result.Ok(id).ToCreatedHttpResult(value => $"/api/v1/things/{value}", value => new ConcurrencyTestSupport.Row(value.ToString(), 0));

        // Assert
        var created = http.ShouldBeOfType<Created<ConcurrencyTestSupport.Row>>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Location.ShouldBe($"/api/v1/things/{id}");
    }

    [Fact]
    public void A_failed_creation_is_the_usual_problem()
    {
        // Act
        var http = Result.Invalid<Guid>("bad", "name", "Name is required.").ToCreatedHttpResult(value => $"/{value}", value => value);

        // Assert
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void A_creation_requires_a_location_and_a_body_selector()
    {
        // Arrange
        var result = Result.Ok(1);

        // Act
        var noLocation = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => result.ToCreatedHttpResult(null!, value => value)));
        var noBody = ShouldAssertionExtensions.ShouldThrow<ArgumentNullException>((Func<object?>)(() => result.ToCreatedHttpResult<int, int>(value => "/", null!)));

        // Assert
        noLocation.ParamName.ShouldBe("location");
        noBody.ParamName.ShouldBe("body");
    }
}
