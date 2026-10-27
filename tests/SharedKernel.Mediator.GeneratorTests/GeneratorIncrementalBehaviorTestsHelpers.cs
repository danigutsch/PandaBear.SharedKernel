using Microsoft.CodeAnalysis;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers for incremental generator caching tests.</summary>
internal static class GeneratorIncrementalBehaviorTestsHelpers
{
    /// <summary>Asserts that the named tracked step ran once, produced outputs, and that every output has one of the expected incremental run reasons.</summary>
    public static void AssertStepHasReason(
        GeneratorDriverRunResult runResult,
        string stepName,
        params IncrementalStepRunReason[] expectedReasons)
    {
        var trackedSteps = runResult.Results.Single().TrackedSteps;
        var step = trackedSteps[stepName].ShouldHaveSingleItem();

        step.Outputs.ShouldNotBeEmpty();
        step.Outputs.ShouldAllSatisfy(output => expectedReasons.ShouldContain(output.Reason));
    }
}
