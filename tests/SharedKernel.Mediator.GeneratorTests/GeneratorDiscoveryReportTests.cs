namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiscoveryReportTests
{
    [Fact]
    public void Generate_Discovery_Report_Single_Project_Counts_Expected_Behavior()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }

            [PipelineOrder(PipelineStage.Validation)]
            public sealed class ValidationBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;

        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("RequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("HandlerCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("PipelineCount = 1;", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Repeated_Runs_Are_Deterministic_Expected_Behavior()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record GetTour(int Id) : IQuery<string>;

            public sealed class GetTourHandler : IQueryHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct) => ValueTask.FromResult("tour");
            }
            """;

        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var firstRun = GeneratorTestHarness.RunGenerator(compilation);
        var secondRun = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Equal(firstRun, secondRun);
    }
}
