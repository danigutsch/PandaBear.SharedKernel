using PandaBear.DispositionHold.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Tests the validation of the disposition hold storage options, whose names are written into SQL text.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.ProviderGuardCategory)]
[Trait(TestTraitNames.CapabilityName, TestTraits.DispositionHoldCapability)]
public sealed class DispositionHoldStorageOptionsTests
{
    private const string Failure = "Disposition hold storage schema and table name must be valid PostgreSQL unquoted identifiers.";

    [Fact]
    public void Defaults_and_63_byte_names_are_accepted()
    {
        // Arrange
        var longest = new string('a', 63);

        // Act
        var defaults = new DispositionHoldStorageOptionsValidator().Validate(null, new DispositionHoldStorageOptions());
        var longestNames = new DispositionHoldStorageOptionsValidator().Validate(null, new DispositionHoldStorageOptions { Schema = longest, TableName = longest });

        // Assert
        defaults.Succeeded.ShouldBeTrue();
        longestNames.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "holds")]
    [InlineData("disposition-hold", "holds")]
    [InlineData("disposition_hold", "holds;drop")]
    [InlineData("disposition_hold", "hölds")]
    [InlineData("disposition_hold", "2holds")]
    public void Invalid_names_are_refused(string schema, string table)
    {
        // Arrange
        var options = new DispositionHoldStorageOptions { Schema = schema, TableName = table };

        // Act
        var result = new DispositionHoldStorageOptionsValidator().Validate(null, options);

        // Assert
        result.FailureMessage.ShouldBe(Failure);
    }

    [Fact]
    public void A_64_byte_name_that_the_server_would_truncate_is_refused()
    {
        // Arrange
        var options = new DispositionHoldStorageOptions { TableName = new string('a', 64) };

        // Act
        var result = new DispositionHoldStorageOptionsValidator().Validate(null, options);

        // Assert
        result.FailureMessage.ShouldBe(Failure);
    }
}
