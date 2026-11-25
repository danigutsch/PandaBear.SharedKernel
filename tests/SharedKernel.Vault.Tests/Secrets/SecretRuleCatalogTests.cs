using SharedKernel.Vault.Secrets;

namespace SharedKernel.Vault.Tests.Secrets;

/// <summary>Tests the catalog of secret rules and the patterns used outside it.</summary>
public sealed class SecretRuleCatalogTests
{
    [Theory]
    [MemberData(nameof(RuleSamples.Ids), MemberType = typeof(RuleSamples))]
    public void Catalog_rule_detects_its_positive_sample(string ruleId)
    {
        // Arrange
        var sample = RuleSamples.All.ShouldHaveSingleItem(entry => entry.RuleId == ruleId);

        // Act
        var findings = ItemScanner.Default.Scan(SecretTestItems.Note("Notes for the record\n" + sample.Positive + "\nend"));

        // Assert
        findings.ShouldContain(finding => finding.RuleId == ruleId);
    }

    [Theory]
    [MemberData(nameof(RuleSamples.Ids), MemberType = typeof(RuleSamples))]
    public void Catalog_rule_ignores_its_negative_sample(string ruleId)
    {
        // Arrange
        var sample = RuleSamples.All.ShouldHaveSingleItem(entry => entry.RuleId == ruleId);

        // Act
        var findings = ItemScanner.Default.Scan(SecretTestItems.Note("Notes for the record\n" + sample.Negative + "\nend"));

        // Assert
        findings.ShouldNotContain(finding => finding.RuleId == ruleId);
    }

    [Fact]
    public void Aws_secret_without_keyword_is_found_within_five_lines_of_an_access_key_ID()
    {
        // Arrange
        string keyId = "AKIA" + TestSecrets.FromAlphabet("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", 16, 130);
        string secret = TestSecrets.FromAlphabet(TestSecrets.Base64, 40, 131);
        string far = TestSecrets.FromAlphabet(TestSecrets.Base64, 40, 132);

        // Act
        var findings = ItemScanner.Default.Scan(SecretTestItems.Note($"{keyId}\nline\n{secret}\n1\n2\n3\n4\n5\n6\n{far}"));

        // Assert
        findings.ShouldHaveSingleItem(finding => finding.RuleId == "aws-secret-access-key");
    }
}
