using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorIncrementalBehaviorTests
{
    [Fact]
    public void Unrelated_edit_keeps_generated_source_step_cached()
    {
        // Arrange
        var initialCompilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader + TestSources.CreateTourWithHandler + TestSources.TourCreatedWithHandler);
        var (driver, _) = GeneratorTestHarness.RunGeneratorDriver(
            initialCompilation,
            globalOptions: null,
            trackIncrementalGeneratorSteps: true);
        var updatedCompilation = initialCompilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace Unrelated; public sealed class Helper { public static int Value => 42; }",
                new CSharpParseOptions(LanguageVersion.Preview),
                cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var (_, updatedRun) = GeneratorTestHarness.RunTrackedGeneratorDriver(driver, updatedCompilation);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "GeneratedSources", IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Whitespace_only_edit_keeps_generated_source_step_cached()
    {
        // Arrange
        var initialSource = TestSources.ModuleHeader + TestSources.CreateTourWithHandler;
        var updatedSource = TestSources.ModuleHeader + """
            public sealed record CreateTour(string Name) : ICommand<int>;


            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }

            """;
        var initialCompilation = GeneratorTestHarness.CreateCompilation(initialSource);
        var (driver, _) = GeneratorTestHarness.RunGeneratorDriver(
            initialCompilation,
            globalOptions: null,
            trackIncrementalGeneratorSteps: true);
        var originalSyntaxTree = Assert.Single(initialCompilation.SyntaxTrees);
        const string defaultUsings = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            """;
        var updatedCompilation = initialCompilation.ReplaceSyntaxTree(
            originalSyntaxTree,
            CSharpSyntaxTree.ParseText(
                defaultUsings + updatedSource,
                (CSharpParseOptions)originalSyntaxTree.Options,
                path: originalSyntaxTree.FilePath,
                cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var (_, updatedRun) = GeneratorTestHarness.RunTrackedGeneratorDriver(driver, updatedCompilation);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "GeneratedSources", IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Adding_a_new_registration_invalidates_discovery_and_generated_source_steps()
    {
        // Arrange
        var initialCompilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader + TestSources.CreateTourWithHandler);
        var (driver, _) = GeneratorTestHarness.RunGeneratorDriver(
            initialCompilation,
            globalOptions: null,
            trackIncrementalGeneratorSteps: true);
        var updatedCompilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader + TestSources.CreateTourWithHandler + TestSources.TourCreatedWithHandler);

        // Act
        var (_, updatedRun) = GeneratorTestHarness.RunTrackedGeneratorDriver(driver, updatedCompilation);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "DiscoveryModel", IncrementalStepRunReason.Modified);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "GeneratedSources", IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void Changing_a_request_response_type_invalidates_discovery_and_generated_source_steps()
    {
        // Arrange
        var initialCompilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader + TestSources.GetTourWithHandler);
        var (driver, _) = GeneratorTestHarness.RunGeneratorDriver(
            initialCompilation,
            globalOptions: null,
            trackIncrementalGeneratorSteps: true);
        var updatedCompilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader + """
            public sealed record GetTour(int Id) : IQuery<int>;

            public sealed class GetTourHandler : IQueryHandler<GetTour, int>
            {
                public ValueTask<int> Handle(GetTour request, CancellationToken ct) => ValueTask.FromResult(request.Id);
            }

            """);

        // Act
        var (_, updatedRun) = GeneratorTestHarness.RunTrackedGeneratorDriver(driver, updatedCompilation);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "DiscoveryModel", IncrementalStepRunReason.Modified);
        GeneratorIncrementalBehaviorTestsHelpers.AssertStepHasReason(updatedRun, "GeneratedSources", IncrementalStepRunReason.Modified);
    }

}
