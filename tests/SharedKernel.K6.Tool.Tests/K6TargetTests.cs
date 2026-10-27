namespace PandaBear.K6.Tool.Tests;

/// <summary>Tests validation and rewriting of the k6 target URL.</summary>
public sealed class K6TargetTests
{
    /// <summary>Verifies that loopback URLs are accepted and normalized by trimming whitespace and the trailing slash.</summary>
    [Theory]
    [InlineData("http://localhost:5000", "http://localhost:5000")]
    [InlineData("https://localhost:5001/", "https://localhost:5001")]
    [InlineData("http://127.0.0.1:5000/api/", "http://127.0.0.1:5000/api")]
    [InlineData("https://[::1]:5001", "https://[::1]:5001")]
    [InlineData(" http://127.0.0.2:8080 ", "http://127.0.0.2:8080")]
    public void Allows_loopback_targets(string value, string expected)
    {
        // Act
        var target = K6Target.Parse(value, "APP_BASE_URL", allowExternal: false);

        // Assert
        K6Target.Format(target).ShouldBe(expected);
    }

    /// <summary>Verifies that non-loopback targets are rejected unless external targets are allowed.</summary>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://host.docker.internal:5000")]
    [InlineData("https://mylocalhost.example:8443/path")]
    [InlineData("https://127.0.0.1.example:8443/path")]
    [InlineData("http://0.0.0.0:5000")]
    public void Rejects_external_targets_unless_allowed(string value)
    {
        // Act
        var rejected = Record.Exception(() => K6Target.Parse(value, "APP_BASE_URL", allowExternal: false));
        var allowed = Record.Exception(() => K6Target.Parse(value, "APP_BASE_URL", allowExternal: true));

        // Assert
        rejected.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("APP_BASE_URL must target a loopback host such as localhost, 127.0.0.1, or [::1] unless --allow-external-target is passed.");
        allowed.ShouldBeNull();
    }

    /// <summary>Verifies that user information and non-HTTP URLs are rejected even when external targets are allowed.</summary>
    [Theory]
    [InlineData("http://localhost:80@evil.example", "APP_BASE_URL must not include user information.")]
    [InlineData("ftp://localhost", "APP_BASE_URL must be an absolute http:// or https:// URL.")]
    [InlineData("localhost:5000", "APP_BASE_URL must be an absolute http:// or https:// URL.")]
    [InlineData("", "APP_BASE_URL must be an absolute http:// or https:// URL.")]
    [InlineData("http://localhost:5000/?debug=1", "APP_BASE_URL must not include a query or fragment.")]
    [InlineData("http://localhost:5000/#top", "APP_BASE_URL must not include a query or fragment.")]
    public void Rejects_malformed_targets_even_when_external_targets_are_allowed(string value, string message)
    {
        // Act
        var exception = Record.Exception(() => K6Target.Parse(value, "APP_BASE_URL", allowExternal: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(message);
    }

    /// <summary>Verifies that loopback and unspecified hosts are rewritten to <c>host.docker.internal</c> for containers.</summary>
    [Theory]
    [InlineData("http://localhost:5000", "http://host.docker.internal:5000")]
    [InlineData("http://127.0.0.1:5000/api/", "http://host.docker.internal:5000/api")]
    [InlineData("http://0.0.0.0:5000", "http://host.docker.internal:5000")]
    [InlineData("http://[::1]:5000", "http://host.docker.internal:5000")]
    [InlineData("http://[::]:5000", "http://host.docker.internal:5000")]
    [InlineData("https://mylocalhost.example:8443/path", "https://mylocalhost.example:8443/path")]
    [InlineData("https://127.0.0.1.example:8443/path", "https://127.0.0.1.example:8443/path")]
    public void Container_targets_reach_loopback_hosts_through_host_docker_internal(string value, string expected)
    {
        // Act
        var target = K6Target.ForContainer(new Uri(value, UriKind.Absolute));

        // Assert
        target.ShouldBe(expected);
    }

    /// <summary>Verifies that unspecified host addresses become <c>127.0.0.1</c> and other values are left unchanged.</summary>
    [Theory]
    [InlineData("http://0.0.0.0:5000", "http://127.0.0.1:5000/")]
    [InlineData("http://[::]:5000/", "http://127.0.0.1:5000/")]
    [InlineData("http://localhost:5000", "http://localhost:5000/")]
    [InlineData("not a url", "not a url")]
    public void Unspecified_host_addresses_become_the_loopback_address(string value, string expected)
    {
        // Act
        var target = K6Target.ToLoopback(value);

        // Assert
        target.ShouldBe(expected);
    }
}
