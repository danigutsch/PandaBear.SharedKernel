namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DependencyInjectionCapability)]
public sealed class GeneratorDependencyInjectionTests
{
    [Fact]
    public void Generate_Service_Registration_Single_Project_Expected_Behavior()
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

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed record StreamTours() : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    yield return "tour";
                    await Task.CompletedTask;
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(
            compilation,
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("public static partial class SharedKernelMediatorServiceCollectionExtensions", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.ICommandHandler<global::Demo.CreateTour, int>, global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IPipelineBehavior<global::Demo.CreateTour, int>, global::Demo.ValidationBehavior>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.INotificationHandler<global::Demo.TourCreated>, global::Demo.TourCreatedHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IStreamRequestHandler<global::Demo.StreamTours, string>, global::Demo.StreamToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Marked_Module_Assembly_Included_Expected_Behavior()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            public sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(moduleSource, "SharedKernel.Mediator.Tests.ModuleA");
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: [moduleReference]);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(
            compilation,
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("services.AddTransient<global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IQueryHandler<global::ModuleA.SearchTours, int>, global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }
}
