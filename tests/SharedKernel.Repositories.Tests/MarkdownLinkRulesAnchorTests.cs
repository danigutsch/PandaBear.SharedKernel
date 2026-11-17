namespace PandaBear.Repositories.Tests;

/// <summary>
/// Pins the heading anchors of <see cref="MarkdownLinkRules.Anchors"/> to the ones GitHub generates. The first cases are
/// fixtures of github-slugger, which reproduces GitHub's heading anchors.
/// </summary>
public sealed class MarkdownLinkRulesAnchorTests
{
    [Theory]
    [MemberData(nameof(Headings))]
    public void A_heading_gets_the_anchor_that_github_gives_it(string heading, string expected)
    {
        // Act
        var anchors = MarkdownLinkRules.Anchors("# " + heading + "\n");

        // Assert
        anchors.ShouldHaveSingleItem().ShouldBe(expected);
    }

    /// <summary>Gets headings and the anchor GitHub gives each of them.</summary>
    public static TheoryData<string, string> Headings => new()
    {
        { "heading with a period.txt", "heading-with-a-periodtxt" },
        { "exchange.bind_headers(exchange, routing [, bindCallback])", "exchangebind_headersexchange-routing--bindcallback" },
        { "\U0001F604 emoji", "-emoji" },
        { "ASP.NET Core error and security telemetry", "aspnet-core-error-and-security-telemetry" },
        { "Version 1.2 — Café", "version-12--café" },
        { "Configuração do serviço", "configuração-do-serviço" },
        { "Ünïcode İstanbul KelvinK", "ünïcode-i̇stanbul-kelvink" },
        { "TOOL_PACKAGE_ID", "tool_package_id" },
        { "The `__init__` method", "the-__init__-method" },
        { "_Emphasis_ and __strong__ text", "emphasis-and-strong-text" },
        { "<b>Tagged</b> `code` *Emph*", "tagged-code-emph" },
    };
}
