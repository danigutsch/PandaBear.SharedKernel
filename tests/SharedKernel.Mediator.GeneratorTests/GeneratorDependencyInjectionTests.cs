using System.Globalization;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DependencyInjectionCapability)]
public sealed class GeneratorDependencyInjectionTests
{
    [Fact]
    public void Generate_service_registration_bootstrap_only_emits_no_transient_registrations()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(TestSources.ModuleHeader);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Empty(diagnostics);
        Assert.Contains("services.AddMetrics();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<AppMediatorInstrumentation>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<AppMediator>();", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("services.AddTransient<", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_service_registration_single_project()
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
    public void Generate_service_registration_open_generic_pipeline_is_closed_per_request()
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
    public void Generate_service_registration_stream_pipeline_is_closed_per_stream_request()
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
    public void Generate_service_registration_marked_module_assembly_included()
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
    public void Generate_service_registration_notification_handler_from_a_separate_source_file()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(
            [
                TestSources.ModuleHeader + "public sealed record TourCreated(int Id) : INotification;",
                TestSources.DemoHeader
                + """
                public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
                {
                    public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
                }
                """
            ]);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Empty(diagnostics);
        Assert.Contains("services.AddTransient<global::Demo.TourCreatedHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.INotificationHandler<global::Demo.TourCreated>, global::Demo.TourCreatedHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_service_registration_stream_handler_from_a_separate_source_file()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(
            [
                TestSources.ModuleHeader
                + """
                public sealed record StreamTours() : IStreamRequest<string>;
                """,
                TestSources.DemoHeader
                + """
                public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
                {
                    public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                    {
                        yield return "tour";
                        await Task.CompletedTask;
                    }
                }
                """
            ]);

        // Act
        var (generatedSource, diagnostics) = GeneratorTestHarness.RunAndGetResult(
            compilation,
            GeneratedHintNames.DependencyInjection);

        // Assert
        Assert.Empty(diagnostics);
        Assert.Contains("services.AddTransient<global::Demo.StreamToursHandler>();", generatedSource, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.IStreamRequestHandler<global::Demo.StreamTours, string>, global::Demo.StreamToursHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_service_registration_internal_handler_in_primary_assembly()
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
    public void Generate_service_registration_internal_handler_from_marked_module_diagnostic()
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
    public void Generate_service_registration_internal_handler_from_marked_module_with_internalsvisibleto()
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
    public void Generate_service_registration_unmarked_module_diagnostic()
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
    public void Generate_service_registration_duplicate_self_registration_diagnostic()
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

    [Fact]
    public void Generate_service_registration_duplicate_self_registration_diagnostic_for_stream_handlers()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + """
            public sealed record StreamTours() : IStreamRequest<string>;
            public sealed record StreamCities() : IStreamRequest<string>;

            public sealed class SearchStreamHandler : IStreamRequestHandler<StreamTours, string>, IStreamRequestHandler<StreamCities, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    yield return "tour";
                    await Task.CompletedTask;
                }

                public async IAsyncEnumerable<string> Handle(StreamCities request, CancellationToken ct)
                {
                    yield return "city";
                    await Task.CompletedTask;
                }
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
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.SearchStreamHandler", StringComparison.Ordinal));
        Assert.Equal(
            1,
            generatedSource.Split("services.AddTransient<global::Demo.SearchStreamHandler>();", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.IStreamRequestHandler<global::Demo.StreamTours, string>, global::Demo.SearchStreamHandler>();",
            generatedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "services.AddTransient<global::SharedKernel.Mediator.IStreamRequestHandler<global::Demo.StreamCities, string>, global::Demo.SearchStreamHandler>();",
            generatedSource,
            StringComparison.Ordinal);
    }
}
