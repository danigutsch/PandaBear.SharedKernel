using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

public sealed class VaultCommandEnvironmentTests
{
    [Fact]
    public void FromConsole_reports_whether_input_is_a_terminal_and_uses_the_system_clock_without_prompting()
    {
        // Act
        var environment = VaultCommandEnvironment.FromConsole();

        // Assert
        environment.IsInteractive.ShouldBe(!Console.IsInputRedirected);
        environment.Time.ShouldBeSameAs(TimeProvider.System);
    }
}
