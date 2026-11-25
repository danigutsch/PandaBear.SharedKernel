namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Verifies the existing filter used to select database integration tests.</summary>
public sealed class IntegrationTestTraitValuesTests
{
    [Fact]
    public static void Database_integration_keeps_its_existing_filter_key_and_value()
    {
        // Act, Assert
        SharedKernelTestTraitNames.CategoryName.ShouldBe("TestCategory");
        IntegrationTestTraitValues.DatabaseIntegrationCategory.ShouldBe("database-integration");
    }
}
