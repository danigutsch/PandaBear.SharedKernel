using System.Text.RegularExpressions;

namespace PandaBear.SecretScanning.Tests;

/// <summary>Tests the shape of the rule catalog: every rule has a sample, a source, and keywords, and every pattern a one-second timeout.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.SecretScanningCapability)]
public sealed class SecretRuleCatalogTests
{
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
}
