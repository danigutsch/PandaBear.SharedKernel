namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests that Pix email keys accept exactly the values of the DICT email pattern.
/// </summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "PaymentIdentifiers")]
public sealed class PixKeyEmailEquivalenceTests
{
    [Fact]
    public void Email_keys_match_the_dict_email_pattern_for_generated_candidates()
    {
        // Arrange
        var candidates = PixKeyEmailCandidates.All(seed: 20261115, count: 50_000);

        // Act
        var mismatches = candidates
            .Where(static value => PixKey.Create(PixKeyType.Email, value).IsSuccess != PixKeyEmailCandidates.IsDictEmail(value))
            .Take(5)
            .ToArray();

        // Assert
        mismatches.ShouldBeEmpty();
    }
}
