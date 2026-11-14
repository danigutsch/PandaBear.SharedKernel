namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Tests that a response whose Content-Security-Policy carries the request's nonce never leaves as publicly cacheable, whichever order the
/// security headers and the default public caching are registered in.
/// </summary>
public sealed class SecurityHeadersNonceCachingTests
{
    private const string NoncePolicy = "default-src 'self'; style-src 'self' 'nonce-{nonce}'";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_nonce_page_is_never_public_whatever_the_registration_order(bool securityHeadersFirst)
    {
        // Arrange
        await using var host = await NonceCachingTestHost.Start(NoncePolicy, securityHeadersFirst);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/page");

        // Assert
        NonceTestHost.Header(response).ShouldContain("'nonce-", StringComparison.Ordinal);
        var shared = response.Headers.CacheControl is { Public: true } or { SharedMaxAge: not null };
        shared.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_nonce_page_that_sets_its_own_public_header_becomes_private_and_keeps_its_lifetime(bool securityHeadersFirst)
    {
        // Arrange
        await using var host = await NonceCachingTestHost.Start(NoncePolicy, securityHeadersFirst);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/own-public");

        // Assert
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.Public.ShouldBeFalse();
        cacheControl.Private.ShouldBeTrue();
        cacheControl.SharedMaxAge.ShouldBeNull();
        cacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(300));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_page_whose_policy_has_no_nonce_stays_public(bool securityHeadersFirst)
    {
        // Arrange
        await using var host = await NonceCachingTestHost.Start("default-src 'self'", securityHeadersFirst);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/page");

        // Assert
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe("public, max-age=60");
    }
}
