namespace PandaBear.K6.Tool;

/// <summary>The parsed options of <c>run</c>.</summary>
/// <param name="Root">The absolute repository root.</param>
/// <param name="Scenario">The scenario name.</param>
/// <param name="Container">Whether k6 runs in the configured container engine.</param>
/// <param name="Image">The image that replaces the configured one.</param>
/// <param name="AllowExternalTarget">Whether the target may be a host other than a loopback host.</param>
/// <param name="AllowHttpDebug">Whether <c>--http-debug</c> may reach k6.</param>
/// <param name="AllowRemoteOutput">Whether custom k6 outputs may reach k6.</param>
/// <param name="K6Arguments">The arguments that reach <c>k6 run</c> before the script.</param>
internal sealed record K6RunOptions(
    string Root,
    string Scenario,
    bool Container,
    string? Image,
    bool AllowExternalTarget,
    bool AllowHttpDebug,
    bool AllowRemoteOutput,
    string[] K6Arguments);
