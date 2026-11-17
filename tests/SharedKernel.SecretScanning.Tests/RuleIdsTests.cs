using System.Reflection;

namespace PandaBear.SecretScanning.Tests;

/// <summary>Tests that the scanner lists every rule identifier it can report, so a caller can check its own map against them.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.SecretScanningCapability)]
public sealed class RuleIdsTests
{
    [Fact]
    public void Rule_IDs_are_the_catalog_rules_and_the_detectors_and_never_overlap()
    {
        // Arrange
        string[] catalog = [.. SecretRuleCatalog.All.Select(rule => rule.Id)];

        // Act
        var ids = SecretScanner.Default.RuleIds;

        // Assert
        ids.Count.ShouldBe(catalog.Length + DetectorRuleIds.All.Count);
        ids.Order(StringComparer.Ordinal).ShouldBe(catalog.Concat(DetectorRuleIds.All).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_detector_rule_ID_is_listed()
    {
        // Act
        var constants = typeof(DetectorRuleIds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        // Assert
        constants.Order(StringComparer.Ordinal).ShouldBe(DetectorRuleIds.All.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(RuleSamples.Ids), MemberType = typeof(RuleSamples))]
    public void A_catalog_sample_reports_only_listed_rules(string ruleId)
    {
        // Arrange
        var sample = RuleSamples.All.ShouldHaveSingleItem(entry => entry.RuleId == ruleId);

        // Act
        var reported = SecretScanner.Default.Scan(sample.Positive, "notes", SecretScanContext.None).Select(found => found.RuleId);

        // Assert
        reported.ShouldContain(ruleId);
        reported.Except(SecretScanner.Default.RuleIds).ShouldBeEmpty();
    }

    [Fact]
    public void The_structural_detectors_report_only_listed_rules()
    {
        // Arrange
        string text = string.Join(
            '\n',
            "password: tq8-Vw2#lm",
            "PIN: 4821",
            "username: alice.example",
            "recovery code: " + string.Concat(Enumerable.Repeat("k3m9", 8)),
            "IBAN DE89 3704 0044 0532 0130 00",
            "card 4111 1111 1111 1111",
            "Security question: first pet? Answer: biscuit");
        var context = new SecretScanContext { ExpectsUsername = true, IsBitwardenAccount = true };

        // Act
        var reported = SecretScanner.Default.Scan(text, "notes", context).Select(found => found.RuleId).ToList();

        // Assert
        reported.Count.ShouldBeGreaterThan(4);
        reported.Except(SecretScanner.Default.RuleIds).ShouldBeEmpty();
    }
}
