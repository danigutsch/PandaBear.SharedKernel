namespace SharedKernel.AuditTrail.GeneratorTests;

public sealed class AuditTrailPackageConsumptionTests(AuditTrailPackageFeedFixture packageFeed)
    : IClassFixture<AuditTrailPackageFeedFixture>
{
    [Fact]
    public async Task Source_generator_package_supplies_every_generated_code_dependency()
    {
        // Arrange
        using var workspace = new AuditTrailPackageConsumptionWorkspace(packageFeed);

        // Act
        var buildOutput = await workspace.Build();
        var generatedFiles = workspace.GetGeneratedFiles(AuditTrailGeneratorTestHarness.GeneratedHintName);

        // Assert
        buildOutput.ShouldContain("Build succeeded.", StringComparison.Ordinal);
        generatedFiles.ShouldHaveSingleItem();
    }
}
