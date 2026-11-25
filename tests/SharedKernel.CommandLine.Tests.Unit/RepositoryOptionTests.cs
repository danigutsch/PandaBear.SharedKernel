namespace SharedKernel.CommandLine.Tests;

/// <summary>Tests the shared <c>--repository</c> option.</summary>
public sealed class RepositoryOptionTests
{
    [Theory]
    [InlineData("danigutsch/PandaBear.SharedKernel")]
    [InlineData("my-org.x/repo_1.v2")]
    [InlineData("dani_gutsch/name")]
    public void An_owner_and_name_is_accepted(string repository)
    {
        // Arrange
        var option = new RepositoryOption("Repository.");

        // Act
        var result = CommandParsing.Parse([option], "--repository", repository);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.GetValue(option).ShouldBe(repository);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/")]
    [InlineData("/name")]
    [InlineData("owner/name/extra")]
    [InlineData("owner/..")]
    [InlineData("owner/.")]
    [InlineData("../name")]
    [InlineData("owner/na me")]
    [InlineData("owner/name?x=1")]
    [InlineData("owner/name\n")]
    [InlineData("https://github.com/owner/name")]
    public void Anything_that_could_change_a_request_address_is_a_parse_error(string repository)
    {
        // Arrange
        var option = new RepositoryOption("Repository.");

        // Act
        var result = CommandParsing.Parse([option], "--repository", repository);

        // Assert
        CommandParsing.Errors(result).ShouldBe([RepositoryOption.InvalidMessage]);
    }

    [Fact]
    public void The_option_is_named_repository_and_keeps_the_description()
    {
        // Act
        var option = new RepositoryOption("GitHub repository.");

        // Assert
        option.Name.ShouldBe("--repository");
        option.HelpName.ShouldBe("owner/name");
        option.Description.ShouldBe("GitHub repository.");
        option.Required.ShouldBeFalse();
    }
}
