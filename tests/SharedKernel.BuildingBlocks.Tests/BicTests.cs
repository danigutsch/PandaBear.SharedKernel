using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests creation and validation of Bic business identifier codes.
/// </summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class BicTests
{
    [Theory]
    [InlineData("DEUTDEFF")]
    [InlineData("DEUTDEFF500")]
    [InlineData("NWBKGB2L")]
    [InlineData("AB12BR00")]
    public void Creates_eight_or_eleven_character_iso_syntax(string value)
    {
        // Act
        var result = Bic.Create(value);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deutdeff")]
    [InlineData("DEUTDEFF\n")]
    [InlineData("DEUTDEFF ")]
    [InlineData("DEUTDE")]
    [InlineData("DEUTDEFF50")]
    [InlineData("DEUTDEFF5000")]
    [InlineData("DEUTD3FF")]
    [InlineData("DEUT1EFF")]
    [InlineData("DEUTDEＦF")]
    [InlineData("DEUTDE-F")]
    public void Rejects_noncanonical_or_malformed_candidates(string? value)
    {
        // Act
        var result = Bic.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }
}
