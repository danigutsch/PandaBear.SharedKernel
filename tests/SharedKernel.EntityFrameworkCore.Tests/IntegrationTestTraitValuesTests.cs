namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Verifies the shared filter used to select database integration tests.</summary>
public sealed class IntegrationTestTraitValuesTests
{
    [Fact]
    public static void Database_integration_keeps_its_existing_filter_key_and_value()
    {
        // Act, Assert
        TestTraitNames.CategoryName.ShouldBe("Category");
        IntegrationTestTraitValues.DatabaseIntegrationCategory.ShouldBe("database-integration");
    }
}
