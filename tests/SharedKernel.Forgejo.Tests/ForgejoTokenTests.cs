using System.Diagnostics;

namespace PandaBear.Forgejo.Tests;

/// <summary>Tests reading the Forgejo token and keeping it from child processes.</summary>
public sealed class ForgejoTokenTests
{
    [Theory]
    [InlineData("token", "token")]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void A_non_blank_token_variable_is_the_token(string? value, string? expected)
    {
        // Act
        var token = ForgejoToken.Find(name => name == "FORGEJO_TOKEN" ? value : "other");

        // Assert
        token.ShouldBe(expected);
    }

    [Theory]
    [InlineData("FORGEJO_TOKEN", true)]
    [InlineData("forgejo_token", true)]
    [InlineData("GITHUB_TOKEN", false)]
    [InlineData("FORGEJO_SERVER_URL", false)]
    public void Token_variables_are_recognized_in_any_case(string name, bool expected) =>
        ForgejoToken.IsTokenVariable(name).ShouldBe(expected);

    [Fact]
    public void The_token_is_removed_from_a_child_environment_in_any_case()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("child");
        startInfo.Environment["FORGEJO_TOKEN"] = "secret";
        startInfo.Environment["forgejo_token"] = "secret";
        startInfo.Environment["FORGEJO_SERVER_URL"] = "https://codeberg.org";

        // Act
        ForgejoToken.RemoveFrom(startInfo);

        // Assert
        startInfo.Environment.Keys.ShouldNotContain("FORGEJO_TOKEN");
        startInfo.Environment.Keys.ShouldNotContain("forgejo_token");
        startInfo.Environment["FORGEJO_SERVER_URL"].ShouldBe("https://codeberg.org");
    }
}
