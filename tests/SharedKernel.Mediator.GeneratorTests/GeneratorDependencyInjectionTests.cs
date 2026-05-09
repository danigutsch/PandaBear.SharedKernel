using System.Globalization;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DependencyInjectionCapability)]
public sealed class GeneratorDependencyInjectionTests
{
    [Fact]
    public void Generate_Service_Registration_Single_Project()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + TestSources.CreateTourValidationBehavior
            + TestSources.TourCreatedWithHandler
            + TestSources.StreamToursWithHandler;

        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(
            source,
            GeneratedHintNames.DependencyInjection);

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
    public void Generate_Service_Registration_Open_Generic_Pipeline_Is_Closed_Per_Request()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + """
            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct) => next();
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(
            source,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Contains("services.AddTransient<global::Demo.ValidationBehavior<global::Demo.CreateTour, int>>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IPipelineBehavior<global::Demo.CreateTour, int>, global::Demo.ValidationBehavior<global::Demo.CreateTour, int>>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Stream_Pipeline_Is_Closed_Per_Stream_Request()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.StreamToursWithHandler
            + """
            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
                where TRequest : IStreamRequest<TResponse>
            {
                public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerContinuation<TResponse> next, CancellationToken ct) => next();
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(
            source,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Contains("services.AddTransient<global::Demo.ValidationBehavior<global::Demo.StreamTours, string>>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IStreamPipelineBehavior<global::Demo.StreamTours, string>, global::Demo.ValidationBehavior<global::Demo.StreamTours, string>>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Marked_Module_Assembly_Included()
    {
        // Arrange
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(TestSources.ModuleAMarkedSource, "SharedKernel.Mediator.Tests.ModuleA");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(
            source,
            GeneratedHintNames.DependencyInjection,
            additionalReferences: [moduleReference]);

        // Assert
        Assert.Contains("services.AddTransient<global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<global::SharedKernel.Mediator.IQueryHandler<global::ModuleA.SearchTours, int>, global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Internal_Handler_In_Primary_Assembly()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + """
            public sealed record CreateTour(string Name) : ICommand<int>;

            internal sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Contains("services.AddTransient<global::Demo.CreateTourHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Internal_Handler_From_Marked_Module_Diagnostic()
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
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: [moduleReference]);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InaccessibleRegistrationType
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::ModuleA.SearchToursHandler", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "services.AddTransient<global::SharedKernel.Mediator.IQueryHandler<global::ModuleA.SearchTours, int>, global::ModuleA.SearchToursHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Internal_Handler_From_Marked_Module_With_InternalsVisibleTo()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]
            [assembly: InternalsVisibleTo("SharedKernel.Mediator.Tests.Dynamic")]

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            internal sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(moduleSource, "SharedKernel.Mediator.Tests.ModuleA");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: [moduleReference]);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.IQueryHandler<global::ModuleA.SearchTours, int>, global::ModuleA.SearchToursHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Unmarked_Module_Diagnostic()
    {
        // Arrange
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(TestSources.ModuleAUnmarkedSource, "SharedKernel.Mediator.Tests.ModuleA.Unmarked");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: [moduleReference]);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingModuleMarker
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains(
                                     "SharedKernel.Mediator.Tests.ModuleA.Unmarked",
                                     StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Service_Registration_Duplicate_Self_Registration_Diagnostic()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + """
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
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.DuplicateGeneratedRegistration
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
