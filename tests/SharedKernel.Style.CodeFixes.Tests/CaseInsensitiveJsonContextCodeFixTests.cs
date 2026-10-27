namespace PandaBear.Style.CodeFixes.Tests;

/// <summary>Tests the SKSTYLE015 code fix that makes a JSON serializer context case-insensitive for property names.</summary>
public sealed class CaseInsensitiveJsonContextCodeFixTests
{
    private const string DiagnosticId = "SKSTYLE015";

    [Theory]
    [InlineData("", "[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(WriteIndented = true)]", "[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(JsonSerializerDefaults.General, WriteIndented = true)]", "[JsonSourceGenerationOptions(JsonSerializerDefaults.General, WriteIndented = true, PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]", "[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false, WriteIndented = true)]", "[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]")]
    public async Task Fix_adds_or_updates_the_option_and_preserves_other_options(string existing, string expected)
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create($$"""
            using System.Text.Json;
            using System.Text.Json.Serialization;
            public sealed record Payload(string Name);
            {{existing}}
            [JsonSerializable(typeof(Payload))]
            internal sealed partial class GitHubJsonContext : JsonSerializerContext;
            """);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "GitHubJsonContext");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var changed = await workspace.GetDocumentText();

        // Assert
        changed.ShouldContain(expected, StringComparison.Ordinal);
        changed.Split("JsonSourceGenerationOptions").Length.ShouldBe(2);
        changed.ShouldContain("[JsonSerializable(typeof(Payload))]", StringComparison.Ordinal);
    }
}
