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
        var step = TestAssert.ExactlyOne(trackedSteps[stepName]);

        TestAssert.NotEmpty(step.Outputs);
        TestAssert.All(step.Outputs, output => TestAssert.Contains(output.Reason, expectedReasons));
    }
}
