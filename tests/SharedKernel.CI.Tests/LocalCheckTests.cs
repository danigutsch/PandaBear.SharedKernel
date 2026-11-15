namespace PandaBear.CI.Tests;

/// <summary>Guards the report of a local check: what it says it does not check.</summary>
public sealed class LocalCheckTests
{
    [Fact]
    public async Task All_lanes_passing_completes_the_check_and_the_report_says_what_it_does_not_check()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        await new LocalCheckRunner(processes.Machine()).Run(
            repository.Root,
            Path.Combine(repository.Root, "TestResults", "local-check"),
            new LocalCheckOptions("origin/main", "Debug", false, [], TimeSpan.FromMinutes(30)),
            output,
            error,
            ct);
        var result = (Output: output.ToString(), Error: error.ToString());

        // Assert
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("passed", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("passed", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("Core.Tests", StringComparison.Ordinal);
        result.Output.ShouldContain("This lane does not check:", StringComparison.Ordinal);
        foreach (var item in LocalCheck.NotCovered)
        {
            result.Output.ShouldContain(item, StringComparison.Ordinal);
        }

        result.Output.ShouldContain("this is not the whole of CI", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
    }
}
