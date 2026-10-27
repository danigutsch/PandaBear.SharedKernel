using System.Text.RegularExpressions;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests the catalog of secret rules and the patterns used outside it.</summary>
public sealed class SecretRuleCatalogTests
{
    /// <summary>Each catalog rule detects its positive sample.</summary>
    [Theory]
    [MemberData(nameof(RuleSamples.Ids), MemberType = typeof(RuleSamples))]
    public void Catalog_rule_detects_its_positive_sample(string ruleId)
    {
        // Arrange
        var sample = RuleSamples.All.ShouldHaveSingleItem(entry => entry.RuleId == ruleId);

        // Act
        var findings = SecretScanner.Default.Scan(SecretTestItems.Note("Notes for the record\n" + sample.Positive + "\nend"));

        // Assert
        findings.ShouldContain(finding => finding.RuleId == ruleId);
    }

    /// <summary>Each catalog rule ignores its negative sample.</summary>
    [Theory]
    [MemberData(nameof(RuleSamples.Ids), MemberType = typeof(RuleSamples))]
    public void Catalog_rule_ignores_its_negative_sample(string ruleId)
    {
        // Arrange
        var sample = RuleSamples.All.ShouldHaveSingleItem(entry => entry.RuleId == ruleId);

        // Act
        var findings = SecretScanner.Default.Scan(SecretTestItems.Note("Notes for the record\n" + sample.Negative + "\nend"));

        // Assert
        findings.ShouldNotContain(finding => finding.RuleId == ruleId);
    }

    /// <summary>Catalog rule ids are unique and match the samples, and every rule has a source, keywords, and a one-second match timeout.</summary>
    [Fact]
    public void Catalog_rules_all_have_samples_sources_and_keywords()
    {
        // Act
        var ids = SecretRuleCatalog.All.Select(rule => rule.Id).ToList();

        // Assert
        ids.ShouldBe(ids.Distinct().ToList());
        ids.Order(StringComparer.Ordinal).ShouldBe(RuleSamples.All.Select(sample => sample.RuleId).Order(StringComparer.Ordinal));
        SecretRuleCatalog.All.ShouldAllSatisfy(rule =>
        {
            rule.Source.ShouldNotBeEmpty();
            rule.Keywords.ShouldNotBeEmpty();
            rule.Pattern.MatchTimeout.ShouldBe(TimeSpan.FromSeconds(1));
        });
    }

    /// <summary>Patterns used outside the catalog time out only after one second.</summary>
    [Fact]
    public void Patterns_used_outside_the_catalog_time_out_only_after_one_second()
    {
        // Act
        Regex[] patterns =
        [
            SecretPatterns.AwsSecretCandidate(),
            SecretPatterns.MaFileSharedSecret(),
            SecretPatterns.MaFileRevocationCode(),
            SecretPatterns.NotesLine(),
            SecretPatterns.Uuid(),
            SecretPatterns.DateOrTime(),
            SecretPatterns.SemVer(),
            SecretPatterns.Email(),
            SecretPatterns.PlainUrl(),
            SecretPatterns.FilePath(),
            SecretPatterns.TemplatePlaceholder(),
        ];

        // Assert
        patterns.ShouldAllSatisfy(pattern => pattern.MatchTimeout.ShouldBe(TimeSpan.FromSeconds(1)));
    }

    /// <summary>An AWS secret without a keyword is found within five lines of an access key ID and not farther away.</summary>
    [Fact]
    public void Aws_secret_without_keyword_is_found_within_five_lines_of_an_access_key_id()
    {
        // Arrange
        string keyId = "AKIA" + TestSecrets.FromAlphabet("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", 16, 130);
        string secret = TestSecrets.FromAlphabet(TestSecrets.Base64, 40, 131);
        string far = TestSecrets.FromAlphabet(TestSecrets.Base64, 40, 132);

        // Act
        var findings = SecretScanner.Default.Scan(SecretTestItems.Note($"{keyId}\nline\n{secret}\n1\n2\n3\n4\n5\n6\n{far}"));

        // Assert
        findings.ShouldHaveSingleItem(finding => finding.RuleId == "aws-secret-access-key");
    }
}
