namespace SharedKernel.CommandLine.Tests;

/// <summary>Tests the shared <c>--forgejo</c> option.</summary>
public sealed class ForgejoOptionTests
{
    [Theory]
    [InlineData("https://codeberg.org")]
    [InlineData("https://forge.example/sub/path/")]
    public void A_secure_HTTP_address_is_accepted(string server)
    {
        // Arrange
        var option = new ForgejoOption("Forgejo instance.");

        // Act
        var result = CommandParsing.Parse([option], "--forgejo", server);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.GetValue(option).ShouldBe(new Uri(server));
    }

    [Theory]
    [InlineData("")]
    [InlineData("codeberg.org")]
    [InlineData("http://codeberg.org")]
    [InlineData("https://user@codeberg.org")]
    [InlineData("https://codeberg.org/?a=1")]
    [InlineData("https://codeberg.org/#top")]
    public void Anything_else_is_a_parse_error(string server)
    {
        // Arrange
        var option = new ForgejoOption("Forgejo instance.");

        // Act
        var result = CommandParsing.Parse([option], "--forgejo", server);

        // Assert
        CommandParsing.Errors(result).ShouldBe([ForgejoOption.InvalidMessage]);
    }

    [Fact]
    public void The_option_is_named_forgejo_and_keeps_the_description()
    {
        // Act
        var option = new ForgejoOption("Forgejo instance.");

        // Assert
        option.Name.ShouldBe("--forgejo");
        option.HelpName.ShouldBe("url");
        option.Description.ShouldBe("Forgejo instance.");
    }
}
