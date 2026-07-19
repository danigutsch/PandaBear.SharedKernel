using System.Diagnostics;

namespace SharedKernel.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class CancellableChildProcessTests
{
    [Fact]
    public void Reused_process_ids_do_not_match_the_original_child_identity()
    {
        // Arrange
        using var process = Process.GetCurrentProcess();
        var differentStartTime = process.StartTime.ToUniversalTime().AddTicks(1);

        // Act
        var matches = CancellableChildProcess.MatchesIdentity(process, differentStartTime);

        // Assert
        matches.ShouldBeFalse();
    }
}
