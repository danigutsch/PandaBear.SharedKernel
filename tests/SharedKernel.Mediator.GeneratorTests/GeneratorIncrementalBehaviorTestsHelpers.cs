using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorIncrementalBehaviorTestsHelpers
{
    public static void AssertStepHasReason(
        GeneratorDriverRunResult runResult,
        string stepName,
        params IncrementalStepRunReason[] expectedReasons)
    {
        var trackedSteps = runResult.Results.Single().TrackedSteps;
        var step = Assert.Single(trackedSteps[stepName]);

        Assert.NotEmpty(step.Outputs);
        Assert.All(step.Outputs, output => Assert.Contains(output.Reason, expectedReasons));
    }
}
