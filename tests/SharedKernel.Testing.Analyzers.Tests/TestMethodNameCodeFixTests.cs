using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests that the SKTEST002 rename fix writes built-in and configured segments in their listed spelling.</summary>
public sealed class TestMethodNameCodeFixTests
{
    [Theory]
    [InlineData("Parses_json_payload", "Parses_JSON_payload")]
    [InlineData("Parses_the_Json_payload", "Parses_the_JSON_payload")]
    [InlineData("Completes_The_task_from_json", "Completes_the_task_from_JSON")]
    [InlineData("CreatesDbContextForTenant", "Creates_DbContext_for_tenant")]
    [InlineData("RejectsExpiredJwtWithETag", "Rejects_expired_JWT_with_ETag")]
    [InlineData("UsesHTTP2TimeoutFallback", "Uses_HTTP2_timeout_fallback")]
    [InlineData("Ui_renders_the_menu", "UI_renders_the_menu")]
    public async Task Rename_writes_built_in_segments_in_their_listed_spelling(string methodName, string expected)
    {
        // Act
        var updatedText = await TestMethodNameRename.Apply(methodName, properties: null);

        // Assert
        updatedText.ShouldContain($"public void {expected}()", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PublishesIntegrationEventToBff", "Publishes_IntegrationEvent_to_BFF")]
    [InlineData("Returns_Errors_for_the_bff", "Returns_Errors_for_the_BFF")]
    public async Task Rename_writes_configured_segments_in_their_listed_spelling(string methodName, string expected)
    {
        // Arrange
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(TestMethodNameSegments.AdditionalSegmentsProperty, "BFF,IntegrationEvent,Errors");

        // Act
        var updatedText = await TestMethodNameRename.Apply(methodName, properties);

        // Assert
        updatedText.ShouldContain($"public void {expected}()", StringComparison.Ordinal);
    }
}
