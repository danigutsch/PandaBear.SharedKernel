using Microsoft.AspNetCore.Builder;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Tests the per-request Content-Security-Policy nonce: a fresh 128-bit value for every response, the same within one request, written into the
/// policy where it names <see cref="AspNetCoreSecurityHeadersExtensions.NoncePlaceholder" />.
/// </summary>
public sealed class SecurityHeadersNonceTests
{
    private const string Policy = "default-src 'self'; style-src 'self' 'nonce-{nonce}'";

    [Fact]
    public async Task The_nonce_is_the_same_within_a_request_and_in_its_header()
    {
        // Arrange
        await using var host = await NonceTestHost.Start(Policy);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/nonce");

        // Assert
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var values = body.Split('|');
        values[0].ShouldBe(values[1]);
        NonceTestHost.Header(response).ShouldBe($"default-src 'self'; style-src 'self' 'nonce-{values[0]}'");
    }

    [Fact]
    public async Task Every_request_gets_a_new_nonce()
    {
        // Arrange
        await using var host = await NonceTestHost.Start(Policy);

        // Act
        using var first = await host.Send(HttpMethod.Get, "/nonce");
        using var second = await host.Send(HttpMethod.Get, "/nonce");

        // Assert
        NonceTestHost.Header(first).ShouldNotBe(NonceTestHost.Header(second));
    }

    [Fact]
    public async Task The_nonce_is_128_random_bits_in_base64()
    {
        // Arrange
        await using var host = await NonceTestHost.Start(Policy);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/nonce");

        // Assert
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Convert.FromBase64String(body.Split('|')[0]).Length.ShouldBe(16);
    }

    [Fact]
    public async Task A_response_that_never_reads_the_nonce_still_gets_one_in_its_header()
    {
        // Arrange
        await using var host = await NonceTestHost.Start(Policy);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/plain");

        // Assert
        NonceTestHost.Header(response).ShouldStartWith("default-src 'self'; style-src 'self' 'nonce-", StringComparison.Ordinal);
        NonceTestHost.Header(response).ShouldNotContain(AspNetCoreSecurityHeadersExtensions.NoncePlaceholder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_policy_without_the_placeholder_is_written_unchanged()
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.UseSecurityHeaders("default-src 'self'");
            app.MapGet("/plain", () => "ok");
        });

        // Act
        using var response = await host.Send(HttpMethod.Get, "/plain");

        // Assert
        NonceTestHost.Header(response).ShouldBe("default-src 'self'");
    }

    [Fact]
    public async Task A_policy_with_the_placeholder_needs_the_nonce_service()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();

        // Act
        var use = () => app.UseSecurityHeaders(Policy);

        // Assert
        use.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(CspNonceServiceCollectionExtensions.AddCspNonce), StringComparison.Ordinal);
    }
}
