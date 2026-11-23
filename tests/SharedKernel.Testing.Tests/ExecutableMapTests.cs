namespace PandaBear.Testing.Tests;

/// <summary>Tests the executable resolver that a test maps by command name.</summary>
public sealed class ExecutableMapTests
{
    [Fact]
    public void An_empty_map_finds_nothing_until_a_command_is_set()
    {
        // Arrange
        var map = new ExecutableMap();

        // Act
        var before = map.Find("git");
        map.Set("git", "/usr/bin/git");
        var after = map.Find("git");

        // Assert
        before.ShouldBeNull();
        after.ShouldBe("/usr/bin/git");
    }

    [Fact]
    public void A_map_from_pairs_finds_exactly_the_mapped_commands()
    {
        // Arrange
        var map = new ExecutableMap(new Dictionary<string, string> { ["code"] = "/usr/bin/code" });

        // Act
        var found = map.Find("code");
        var otherCase = map.Find("Code");

        // Assert
        found.ShouldBe("/usr/bin/code");
        otherCase.ShouldBeNull();
    }

    [Fact]
    public void The_fallback_answers_unset_commands_and_a_null_entry_hides_a_command()
    {
        // Arrange
        var map = new ExecutableMap(static command => "/fake/" + command);
        map.Set("cargo", null);
        map.Set("npm", "/opt/npm");

        // Act
        var fallback = map.Find("git");
        var hidden = map.Find("cargo");
        var mapped = map.Find("npm");

        // Assert
        fallback.ShouldBe("/fake/git");
        hidden.ShouldBeNull();
        mapped.ShouldBe("/opt/npm");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Find_rejects_a_blank_command_as_the_resolver_contract_says(string command)
    {
        // Arrange
        var map = new ExecutableMap(static _ => "/any");
        Func<object?> find = () => map.Find(command);

        // Act
        var exception = find.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("command");
    }
}
