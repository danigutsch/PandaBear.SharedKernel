namespace SharedKernel.Forgejo.Tests;

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

    [Fact]
    public void Parse_returns_the_owner_and_the_name()
    {
        // Act
        var (owner, name) = ForgejoRepositoryName.Parse("owner/name", "repository");

        // Assert
        owner.ShouldBe("owner");
        name.ShouldBe("name");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/..")]
    [InlineData("owner/name?x=1")]
    public void Parse_rejects_anything_else_with_one_argument_exception(string repository)
    {
        // Act
        Action parse = () => _ = ForgejoRepositoryName.Parse(repository, "repo");

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("repo");
        exception.Message.ShouldBe("The repository must be owner/name, each part of ASCII letters, digits, '-', '.', and '_', and neither part '.' nor '..'. (Parameter 'repo')");
    }

    [Fact]
    public void Parse_rejects_a_null_repository_by_its_parameter_name()
    {
        // Act
        Action parse = () => _ = ForgejoRepositoryName.Parse(null!, "repo");

        // Assert
        parse.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("repo");
    }
}
