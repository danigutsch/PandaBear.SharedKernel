namespace PandaBear.AuditTrail.GeneratorTests;

/// <summary>
/// Tests that the packed audit trail source generator package works in a consumer project.
/// </summary>
/// /// <param name="packageFeed">The fixture that packs the audit trail packages into a local feed.</param>
public sealed class AuditTrailPackageConsumptionTests(AuditTrailPackageFeedFixture packageFeed)
    : IClassFixture<AuditTrailPackageFeedFixture>
{
    [Fact]
    public async Task Source_generator_package_supplies_every_generated_code_dependency()
    {
        // Arrange
        using var workspace = packageFeed.CreateWorkspace("Consumer");
        AuditTrailConsumerProject.Write(workspace);

        // Act
        var buildOutput = await workspace.Build();
        var generatedFiles = workspace.GetGeneratedFiles(AuditTrailGeneratorTestHarness.GeneratedHintName);

        // Assert
        buildOutput.ShouldContain("Build succeeded.", StringComparison.Ordinal);
        generatedFiles.ShouldHaveSingleItem();
    }
}
