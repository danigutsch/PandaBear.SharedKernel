using System.Globalization;

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
        Assert.Contains("services.AddScoped<AppMediator>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<ISender>(static sp => sp.GetRequiredService<AppMediator>());", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<IPublisher>(static sp => sp.GetRequiredService<AppMediator>());", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<IMediator>(static sp => sp.GetRequiredService<AppMediator>());", generatedSource, StringComparison.Ordinal);
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

    [Fact]
    public void Generate_Service_Registration_Internal_Handler_In_Primary_Assembly_Expected_Behavior()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            internal sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs");
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "SKMED010");
        Assert.Contains("services.AddTransient<global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Internal_Handler_From_Marked_Module_Diagnostic_Expected_Behavior()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            internal sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
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
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs");
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == "SKMED010"
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::ModuleA.SearchToursHandler", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "services.AddTransient<global::SharedKernel.Mediator.IQueryHandler<global::ModuleA.SearchTours, int>, global::ModuleA.SearchToursHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Duplicate_Self_Registration_Diagnostic_Expected_Behavior()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;
            public sealed record TourUpdated(int Id) : INotification;

            public sealed class TourEventsHandler : INotificationHandler<TourCreated>, INotificationHandler<TourUpdated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;

                public ValueTask Handle(TourUpdated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs");
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == "SKMED012"
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.TourEventsHandler", StringComparison.Ordinal));
        Assert.Equal(
            1,
            generatedSource.Split("services.AddTransient<global::Demo.TourEventsHandler>();", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.INotificationHandler<global::Demo.TourCreated>, global::Demo.TourEventsHandler>();",
            generatedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.INotificationHandler<global::Demo.TourUpdated>, global::Demo.TourEventsHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }
}
