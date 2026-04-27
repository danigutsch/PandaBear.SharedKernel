namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDispatchTests
{
    [Fact]
    public void Generate_AppMediator_Shell_Expected_Behavior()
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
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(
            compilation,
            "SharedKernel.Mediator.Generated.AppMediator.g.cs");

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("public sealed partial class AppMediator : IMediator", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal global::System.IServiceProvider Services { get; }", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Generated request dispatch is not available yet.", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Generated notification dispatch is not available yet.", generatedSource, StringComparison.Ordinal);
    }
}
