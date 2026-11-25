using Microsoft.AspNetCore.Http;
using SharedKernel.HttpCaching.AspNetCore;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>Tests the cache header of content-addressed responses: public, one year, immutable.</summary>
public sealed class ImmutableCacheHeadersTests
{
    [Fact]
    public void An_immutable_response_is_public_for_a_year_and_never_revalidated()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        HttpCacheHeaders.SetImmutable(context);

        // Assert
        context.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=31536000, immutable");
        HttpCacheHeaders.ImmutableCacheControl.ShouldBe("public, max-age=31536000, immutable");
    }

    [Fact]
    public void An_immutable_response_replaces_an_earlier_cache_header()
    {
        // Arrange
        var context = new DefaultHttpContext();
        HttpCacheHeaders.SetNoStore(context);

        // Act
        HttpCacheHeaders.SetImmutable(context);

        // Assert
        context.Response.Headers.CacheControl.ToString().ShouldBe(HttpCacheHeaders.ImmutableCacheControl);
        context.Response.Headers.Pragma.ShouldBeEmpty();
        context.Response.Headers.Expires.ShouldBeEmpty();
    }

    [Fact]
    public void A_null_context_is_refused()
    {
        // Act
        Action act = () => HttpCacheHeaders.SetImmutable(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
