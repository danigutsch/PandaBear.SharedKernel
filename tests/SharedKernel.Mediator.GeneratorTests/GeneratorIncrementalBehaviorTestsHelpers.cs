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
        var step = (trackedSteps[stepName]).ShouldHaveSingleItem();

        (step.Outputs).ShouldNotBeEmpty();
        (step.Outputs).ShouldAllSatisfy(output => (expectedReasons).ShouldContain(output.Reason));
    }
}
