using PandaBear.Repository.Tool;
using System.Text.Json;

namespace PandaBear.Repository.Tests;

public sealed class PrTitleTests
{
    [Theory]
    [InlineData("feat: support the contract", "BREAKING CHANGE: remove an old field", 0)]
    [InlineData("Update contract", "", 2)]
    [InlineData("feat:    ", "", 2)]
    [InlineData("feat: support the contract", "BREAKING CHANGE missing colon", 2)]
    public void Metadata_rejects_invalid_squash_headers_and_breaking_footers(string title, string body, int expected)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var path = Path.Combine(scope.Root, "event.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            pull_request = new
            {
                title,
                body
            }
        }));
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var result = RepositoryToolApplication.Run(["verify-pr-title", "--event", path], output, error);

        // Assert
        result.ShouldBe(expected);
    }
}
