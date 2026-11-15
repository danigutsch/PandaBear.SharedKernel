namespace PandaBear.Forgejo.Tests;

/// <summary>Tests the check of the <c>owner/name</c> form that becomes part of request addresses.</summary>
public sealed class ForgejoRepositoryNameTests
{
    [Theory]
    [InlineData("owner/name", "owner", "name")]
    [InlineData("my-org.x/repo_1.v2", "my-org.x", "repo_1.v2")]
    public void Owner_and_name_are_split(string repository, string expectedOwner, string expectedName)
    {
        // Act
        var parsed = ForgejoRepositoryName.TryParse(repository, out var owner, out var name);

        // Assert
        parsed.ShouldBeTrue();
        owner.ShouldBe(expectedOwner);
        name.ShouldBe(expectedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("/name")]
    [InlineData("owner/")]
    [InlineData("owner/name/extra")]
    [InlineData("../name")]
    [InlineData("owner/..")]
    [InlineData("owner/.")]
    [InlineData("owner/name?x=1")]
    [InlineData("owner/na me")]
    [InlineData("owner/n%2Fame")]
    [InlineData("ówner/name")]
    public void Anything_else_is_rejected_with_empty_parts(string repository)
    {
        // Act
        var parsed = ForgejoRepositoryName.TryParse(repository, out var owner, out var name);

        // Assert
        parsed.ShouldBeFalse();
        owner.ShouldBeEmpty();
        name.ShouldBeEmpty();
    }

    [Fact]
    public void A_null_repository_is_rejected()
    {
        // Act
        Action parse = () => _ = ForgejoRepositoryName.TryParse(null!, out _, out _);

        // Assert
        parse.ShouldThrow<ArgumentNullException>();
    }
}
