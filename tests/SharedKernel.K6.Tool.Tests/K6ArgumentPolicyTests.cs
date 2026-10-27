namespace PandaBear.K6.Tool.Tests;

/// <summary>Tests the rules that block k6 arguments the tool controls or considers unsafe.</summary>
public sealed class K6ArgumentPolicyTests
{
    private static readonly string[] Controlled = ["APP_BASE_URL", "APP_PROFILE", "K6_NO_USAGE_REPORT"];

    /// <summary>Verifies that <c>--include-system-env-vars</c> is rejected because it would pass the whole environment to the script.</summary>
    [Theory]
    [InlineData("--include-system-env-vars")]
    [InlineData("--include-system-env-vars=false")]
    public void Rejects_system_environment_forwarding(string argument)
    {
        // Arrange
        string[] arguments = [argument];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("--include-system-env-vars is not allowed: it passes the whole k6 environment to the script.");
    }

    /// <summary>Verifies that <c>--http-debug</c> is rejected unless HTTP debugging is explicitly allowed.</summary>
    [Theory]
    [InlineData("--http-debug")]
    [InlineData("--http-debug=full")]
    public void Rejects_http_debug_unless_allowed(string argument)
    {
        // Arrange
        string[] arguments = [argument];

        // Act
        var rejected = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: false, allowRemoteOutput: false));
        var allowed = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: false));

        // Assert
        rejected.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("--http-debug can expose request and response data. Pass --allow-http-debug for explicit local debugging.");
        allowed.ShouldBeNull();
    }

    /// <summary>Verifies that custom k6 outputs are rejected unless remote outputs are explicitly allowed.</summary>
    [Theory]
    [InlineData("--out", "json=leak.json")]
    [InlineData("--out=json=leak.json", null)]
    [InlineData("-o", "json=leak.json")]
    [InlineData("--log-output=loki=https://logs.example", null)]
    [InlineData("--log-output", "file=k6.log")]
    [InlineData("--console-output", "console.log")]
    [InlineData("--traces-output=otel", null)]
    public void Rejects_custom_outputs_unless_allowed(string argument, string? value)
    {
        // Arrange
        string[] arguments = value is null ? [argument] : [argument, value];

        // Act
        var rejected = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: false, allowRemoteOutput: false));
        var allowed = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: false, allowRemoteOutput: true));

        // Assert
        rejected.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("Custom k6 outputs are disabled by default. Pass --allow-remote-output after reviewing the output destination and credentials.");
        allowed.ShouldBeNull();
    }

    /// <summary>Verifies that <c>--summary-export</c> is always rejected because the tool writes the summary itself.</summary>
    [Theory]
    [InlineData("--summary-export")]
    [InlineData("--summary-export=leak.json")]
    public void Rejects_summary_export_even_when_outputs_are_allowed(string argument)
    {
        // Arrange
        string[] arguments = [argument];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("--summary-export is not allowed: the tool writes k6-summary.json to the directory of the run.");
    }

    /// <summary>Verifies that <c>--insecure-skip-tls-verify</c> is rejected.</summary>
    [Theory]
    [InlineData("--insecure-skip-tls-verify")]
    [InlineData("--insecure-skip-tls-verify=true")]
    public void Rejects_insecure_tls_bypass(string argument)
    {
        // Arrange
        string[] arguments = [argument];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("--insecure-skip-tls-verify is not allowed.");
    }

    /// <summary>Verifies that <c>-e</c> and <c>--env</c> cannot set variables the tool takes from the scenario configuration.</summary>
    [Theory]
    [InlineData("-e", "APP_BASE_URL=http://evil.example")]
    [InlineData("--env", "K6_NO_USAGE_REPORT=false")]
    [InlineData("--env=APP_PROFILE=stress", null)]
    [InlineData("-e", "APP_PROFILE")]
    public void Rejects_controlled_environment_overrides(string argument, string? value)
    {
        // Arrange
        string[] arguments = value is null ? [argument] : [argument, value];
        var name = (value ?? argument["--env=".Length..]).Split('=')[0];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe($"-e and --env cannot set {name}, which the tool sets from the scenario configuration.");
    }

    /// <summary>Verifies that environment variables and options the tool does not control pass through.</summary>
    [Theory]
    [InlineData("--env", "K6_TAGS=profile=smoke")]
    [InlineData("-e", "OTHER=1")]
    [InlineData("--vus", "2")]
    [InlineData("-u", "2")]
    public void Allows_uncontrolled_arguments(string argument, string value)
    {
        // Arrange
        string[] arguments = [argument, value];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: false, allowRemoteOutput: false));

        // Assert
        exception.ShouldBeNull();
    }

    /// <summary>Verifies that short options with attached values or bundled letters are rejected.</summary>
    [Theory]
    [InlineData("-eAPP_BASE_URL=http://evil.example")]
    [InlineData("-e=APP_BASE_URL=http://evil.example")]
    [InlineData("-ojson=leak.json")]
    [InlineData("-qo")]
    public void Rejects_short_options_with_attached_values_or_bundles(string argument)
    {
        // Arrange
        string[] arguments = [argument];

        // Act
        var exception = Record.Exception(() => K6ArgumentPolicy.Validate(arguments, Controlled, allowHttpDebug: true, allowRemoteOutput: true));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe($"Pass k6 short options on their own, with any value as the next argument: {argument}");
    }
}
