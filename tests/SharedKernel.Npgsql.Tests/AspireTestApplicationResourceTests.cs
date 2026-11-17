using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Tests what AspireTestApplication tells about the resources of the running application model, with a small executable that never starts
/// (no process, no container).
/// </summary>
public sealed class AspireTestApplicationResourceTests
{
    [Fact]
    public async Task The_application_has_every_resource_of_its_model_started_or_not_and_no_other()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "modelled", "never-written", healthy: true).WithExplicitStart();
        await using var application = await AspireTestApplication.Start(builder, [], TimeSpan.FromMinutes(1), ct);

        // Act
        var modelled = application.HasResource("modelled");
        var otherCase = application.HasResource("MODELLED");
        var missing = application.HasResource("missing");

        // Assert
        modelled.ShouldBeTrue("a resource that never started is still part of the model");
        otherCase.ShouldBeTrue("Aspire resource names are case-insensitive");
        missing.ShouldBeFalse();
    }
}
