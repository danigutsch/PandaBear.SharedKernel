namespace PandaBear.Repository.Tests;

public sealed class SetupScriptTests
{
    [Theory]
    [InlineData(0, 0, 0, 2)]
    [InlineData(23, 0, 23, 1)]
    [InlineData(0, 37, 37, 2)]
    public async Task Native_setup_wrapper_preserves_arguments_root_and_failure_exit_codes(
        int restoreExitCode,
        int buildExitCode,
        int expectedExitCode,
        int expectedCalls)
    {
        // Arrange
        using var scope = new SetupScriptTestScope();

        // Act
        var result = await scope.Run(restoreExitCode, buildExitCode, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(expectedExitCode);
        var calls = await File.ReadAllLinesAsync(scope.LogPath, TestContext.Current.CancellationToken);
        calls.Length.ShouldBe(expectedCalls);
        calls[0].ShouldBe(scope.Root + "|restore SharedKernel.slnx --locked-mode");
        if (expectedCalls == 2)
        {
            calls[1].ShouldBe(scope.Root + "|build SharedKernel.slnx -c Release --no-restore");
        }
    }
}
