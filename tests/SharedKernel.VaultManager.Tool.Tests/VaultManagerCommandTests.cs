namespace PandaBear.VaultManager.Tool.Tests;

public sealed class VaultManagerCommandTests
{
    [Fact]
    public void Root_command_parses_help_without_errors()
    {
        var result = VaultManagerCommand.Create().Parse(["--help"]);

        result.Errors.ShouldBeEmpty();
    }
}
