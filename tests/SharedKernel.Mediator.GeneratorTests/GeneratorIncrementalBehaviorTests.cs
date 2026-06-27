using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorIncrementalBehaviorTests
{
    [Fact]
    public void Unrelated_Edit_Keeps_Generated_Source_Step_Cached()
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
    public void Whitespace_Only_Edit_Keeps_Generated_Source_Step_Cached()
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
    public void Adding_A_New_Registration_Invalidates_Discovery_And_Generated_Source_Steps()
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
    public void Changing_A_Request_Response_Type_Invalidates_Discovery_And_Generated_Source_Steps()
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
