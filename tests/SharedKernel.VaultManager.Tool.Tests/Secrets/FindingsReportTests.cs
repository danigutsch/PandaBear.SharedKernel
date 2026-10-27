using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests the secret findings report.</summary>
public sealed class FindingsReportTests
{
    [Fact]
    public void Findings_report_never_contains_secret_values()
    {
        // Arrange
        string token = TestSecrets.GitHubToken("ghp", 70);
        string card = TestSecrets.CardNumber("4", 16);
        const string Password = "Tr0ub4dor&3x";
        const string Answer = "Rex the dog";
        var item = SecretTestItems.Login(name: "visible item", notes: $"token {token}\nPassword: {Password}\ncard {card}\nQ: First pet?\nA: {Answer}");
        var findings = SecretScanner.Default.Scan(item);

        // Act
        string markdown = FindingsReport.Render([new ItemFindings(item.Name, findings)], ReportFormat.Markdown);
        string text = FindingsReport.Render([new ItemFindings(item.Name, findings)], ReportFormat.Text);

        // Assert
        findings.ShouldHaveCount(4);
        string[] reports = [markdown, text];
        reports.ShouldAllSatisfy(report =>
        {
            report.ShouldContain("visible item", StringComparison.Ordinal);
            report.ShouldContain("ApiToken", StringComparison.Ordinal);
            report.ShouldNotContain(token, StringComparison.Ordinal);
            report.ShouldNotContain(card, StringComparison.Ordinal);
            report.ShouldNotContain(card[..8], StringComparison.Ordinal);
            report.ShouldNotContain(Password, StringComparison.Ordinal);
            report.ShouldNotContain(Answer, StringComparison.Ordinal);
            report.ShouldNotContain("First pet", StringComparison.Ordinal);
        });
        markdown.ShouldContain("| Item |", StringComparison.Ordinal);
    }

    [Fact]
    public void Findings_report_replaces_control_characters_in_item_names_in_both_formats()
    {
        // Arrange
        var item = SecretTestItems.Login(name: "visible\u001b[31m item\u009b", notes: "Password: Tr0ub4dor&3x");
        var findings = SecretScanner.Default.Scan(item);

        // Act
        string markdown = FindingsReport.Render([new ItemFindings(item.Name, findings)], ReportFormat.Markdown);
        string text = FindingsReport.Render([new ItemFindings(item.Name, findings)], ReportFormat.Text);

        // Assert
        markdown.ToCharArray().Where(char.IsControl).ShouldAllSatisfy(character => character.ShouldBe('\n'));
        text.ToCharArray().Where(char.IsControl).ShouldAllSatisfy(character => character.ShouldBeOneOf('\n', '\t'));
        markdown.ShouldContain("| visible\uFFFD[31m item\uFFFD |", StringComparison.Ordinal);
        text.ShouldStartWith("visible\uFFFD[31m item\uFFFD\t", StringComparison.Ordinal);
    }

    [Fact]
    public void Finding_to_string_never_contains_secret_values()
    {
        // Arrange
        string token = TestSecrets.GitHubToken("ghp", 71);
        const string Answer = "Springfield";
        var item = SecretTestItems.Login(notes: $"{token}\nQuestion: City of birth?\nAnswer: {Answer}");

        // Act
        var findings = SecretScanner.Default.Scan(item);

        // Assert
        findings.ShouldHaveCount(2);
        findings.ShouldAllSatisfy(finding =>
        {
            string printed = finding.ToString();
            printed.ShouldNotContain(token, StringComparison.Ordinal);
            printed.ShouldNotContain(Answer, StringComparison.Ordinal);
            printed.ShouldNotContain("City of birth", StringComparison.Ordinal);
            finding.Message.ShouldNotContain(token, StringComparison.Ordinal);
            finding.Location.ToString().ShouldNotContain(token, StringComparison.Ordinal);
        });
    }
}
