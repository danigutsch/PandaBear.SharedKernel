using System.Globalization;
using System.Collections.Immutable;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiscoveryReportTests
{
    [Fact]
    public void Generate_Discovery_Report_Single_Project_Counts()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + TestSources.CreateTourValidationBehavior
            + TestSources.TourCreatedWithHandler
            + TestSources.StreamToursWithHandler;

        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("RequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("HandlerCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("PipelineCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("NotificationCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("StreamRequestCount = 1;", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Call_Graph_Json_Artifact_Is_Not_Emitted_By_Default()
    {
        // Arrange
        var source = TestSources.DemoHeader + TestSources.GetTourWithHandler;

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(source);
        var hasCallGraphSource = runResult.Results.Single().GeneratedSources.Any(
            static source => string.Equals(source.HintName, GeneratedHintNames.CallGraph, StringComparison.Ordinal));

        // Assert
        Assert.False(hasCallGraphSource);
    }

    [Fact]
    public void Generate_Call_Graph_Json_Artifact_When_Enabled()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + TestSources.CreateTourValidationBehavior
            + TestSources.TourCreatedParallelWithHandler
            + TestSources.StreamToursWithHandler;
        var options = ImmutableDictionary<string, string>.Empty.Add("sharedkernel_mediator_emit_call_graph_json", bool.TrueString);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(source, globalOptions: options);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult, GeneratedHintNames.CallGraph);
        var json = GeneratorDiscoveryReportTestsHelpers.LoadGeneratedCallGraphJson(source, generatedSource);

        // Assert
        GeneratorSnapshotVerifier.Verify(json, extension: "json");
        Assert.Contains("\"request\": \"global::Demo.CreateTour\"", json, StringComparison.Ordinal);
        Assert.Contains("\"dispatch\": \"parallel\"", json, StringComparison.Ordinal);
        Assert.Contains("\"request\": \"global::Demo.StreamTours\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Repeated_Runs_Are_Deterministic()
    {
        // Arrange
        var source = TestSources.DemoHeader + TestSources.GetTourWithHandler;

        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var firstRun = GeneratorTestHarness.RunGenerator(compilation);
        var secondRun = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void Generate_Discovery_Report_Marked_Module_Assembly_Included()
    {
        // Arrange
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(TestSources.ModuleAMarkedSource, "SharedKernel.Mediator.Tests.ModuleA");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source, additionalReferences: [moduleReference]);

        // Assert
        Assert.Contains("RequestCount = 2;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("HandlerCount = 2;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ModuleCount = 2;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("SharedKernel.Mediator.Tests.ModuleA | Primary=False | Marker=True", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::ModuleA.SearchTours | Kind=Query", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Unmarked_Module_Assembly_Ignored()
    {
        // Arrange
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(TestSources.ModuleAUnmarkedSource, "SharedKernel.Mediator.Tests.ModuleA.Unmarked");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source, additionalReferences: [moduleReference]);

        // Assert
        Assert.Contains("RequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedKernel.Mediator.Tests.ModuleA.Unmarked", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::ModuleA.SearchTours", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Response_Metadata_Captured()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record TourRow(int Id, string Name);

            public sealed record Page<TItem>(IReadOnlyList<TItem> Items);

            public sealed record ListTours() : IQuery<Page<TourRow>>;

            public sealed class ListToursHandler : IQueryHandler<ListTours, Page<TourRow>>
            {
                public ValueTask<Page<TourRow>> Handle(ListTours request, CancellationToken ct) => ValueTask.FromResult(new Page<TourRow>([]));
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source);

        // Assert
        Assert.Contains("global::Demo.ListTours | Kind=Query", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Response=global::Demo.Page<global::Demo.TourRow>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseGenericDefinition=global::Demo.Page<TItem>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseTypeArguments=[global::Demo.TourRow]", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Result_Like_Wrapper_Shape_Captured()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record Result<TValue>(TValue Value, bool IsSuccess);

            public sealed record GetTour() : IQuery<Result<string>>;

            public sealed class GetTourHandler : IQueryHandler<GetTour, Result<string>>
            {
                public ValueTask<Result<string>> Handle(GetTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(new Result<string>("ok", true));
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains("Response=global::Demo.Result<string>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseGenericDefinition=global::Demo.Result<TValue>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseTypeArguments=[string]", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Without_Mediator_References_Returns_Empty_Model()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class PlainType
            {
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source, includeMediatorReference: false);

        // Assert
        Assert.Contains("RequestCount = 0;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("HandlerCount = 0;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("PipelineCount = 0;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("NotificationCount = 0;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("StreamRequestCount = 0;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public static string[] Modules", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Nested_Plain_Request_Discovered()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed class Container
            {
                public sealed record LookupTour(int Id) : IRequest<string>;

                public sealed class LookupTourHandler : IRequestHandler<LookupTour, string>
                {
                    public ValueTask<string> Handle(LookupTour request, CancellationToken ct) => ValueTask.FromResult("tour");
                }

                [PipelineOrder(PipelineStage.Validation, Order = 20)]
                public sealed class LookupTourValidationBehavior : IPipelineBehavior<LookupTour, string>
                {
                    public ValueTask<string> Handle(LookupTour request, RequestHandlerContinuation<string> next, CancellationToken ct) => next();
                }
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source);

        // Assert
        Assert.Contains("global::Demo.Container.LookupTour | Kind=Request", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.Container.LookupTourHandler(Request,Public,Handle)", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Order=20", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Command_Without_Response_Uses_Unit()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record DeleteTour(int Id) : ICommand;

            public sealed class DeleteTourHandler : ICommandHandler<DeleteTour>
            {
                public ValueTask<Unit> Handle(DeleteTour request, CancellationToken ct) => ValueTask.FromResult(Unit.Value);
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source);

        // Assert
        Assert.Contains("global::Demo.DeleteTour | Kind=Command | Response=global::SharedKernel.Mediator.Unit", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.DeleteTourHandler(Command,Public,Handle)", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Abstract_And_Interface_Shapes_Are_Ignored()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public abstract record AbstractRequest(int Id) : ICommand<int>;

            public abstract class AbstractRequestHandler : ICommandHandler<AbstractRequest, int>
            {
                public abstract ValueTask<int> Handle(AbstractRequest request, CancellationToken ct);
            }

            public sealed record Publishable(int Id) : INotification;

            public interface INotificationShape : INotification
            {
            }

            public abstract class AbstractNotificationHandler : INotificationHandler<Publishable>
            {
                public abstract ValueTask Handle(Publishable notification, CancellationToken ct);
            }

            public sealed class PublishableHandler : INotificationHandler<Publishable>
            {
                public ValueTask Handle(Publishable notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed record StreamTours() : IStreamRequest<string>;

            public abstract class AbstractStreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public abstract IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct);
            }

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    yield return "tour";
                    await Task.CompletedTask;
                }
            }

            public abstract class AbstractPipeline : IPipelineBehavior<AbstractRequest, int>
            {
                public abstract ValueTask<int> Handle(AbstractRequest request, RequestHandlerContinuation<int> next, CancellationToken ct);
            }
            """;
        // Act
        var generatedSource = GeneratorTestHarness.GenerateSource(source);

        // Assert
        Assert.Contains("NotificationCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("StreamRequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.PublishableHandler(Public,Handle)", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractRequest |", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractRequestHandler", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractNotificationHandler", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractStreamToursHandler", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AbstractPipeline", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Demo.INotificationShape", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Pipeline_And_Handler_Order_Is_Deterministic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class ZedCreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(1);
            }

            public sealed class AlphaCreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(2);
            }

            [PipelineOrder(PipelineStage.Transaction, Order = 10)]
            public sealed class TransactionBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }

            [PipelineOrder(PipelineStage.Validation, Order = 30)]
            public sealed class ValidationLateBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationEarlyBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        GeneratorDiscoveryReportTestsHelpers.AssertOrdered(
            generatedSource,
            "global::Demo.AlphaCreateTourHandler(CommandWithResponse,Public,Handle)",
            "global::Demo.ZedCreateTourHandler(CommandWithResponse,Public,Handle)");
        GeneratorDiscoveryReportTestsHelpers.AssertOrdered(
            generatedSource,
            "global::Demo.ValidationEarlyBehavior(Stage=-1000,Order=5,Applicability=Closed,OpenGeneric=<none>)",
            "global::Demo.ValidationLateBehavior(Stage=-1000,Order=30,Applicability=Closed,OpenGeneric=<none>)",
            "global::Demo.TransactionBehavior(Stage=-400,Order=10,Applicability=Closed,OpenGeneric=<none>)");
    }

    [Fact]
    public void Generate_Discovery_Report_Open_Generic_Pipeline_Is_Bound_To_Request_Response_Pair()
    {
        // Arrange
        const string source = TestSources.DemoHeader + TestSources.CreateTourWithHandler + """
            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains(
            "global::Demo.ValidationBehavior<global::Demo.CreateTour, int>(Stage=-1000,Order=10,Applicability=OpenGeneric,OpenGeneric=global::Demo.ValidationBehavior<TRequest, TResponse>)",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Pipeline_Duplicate_Order_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + TestSources.CreateTourWithHandler + """
            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationA : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }

            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationB : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.DuplicatePipelineOrder
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.CreateTour", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Pipeline_Never_Applies_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public abstract record AbstractCreateTour(string Name) : ICommand<int>;

            public sealed class ValidationBehavior : IPipelineBehavior<AbstractCreateTour, int>
            {
                public ValueTask<int> Handle(AbstractCreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.NeverAppliesPipeline
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.ValidationBehavior", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Pipeline_Invalid_Generic_Arity_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + TestSources.CreateTourWithHandler + """
            public sealed class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest, int>
                where TRequest : IRequest<int>
            {
                public ValueTask<int> Handle(TRequest request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidPipelineGenericArity
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.ValidationBehavior<TRequest>", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Pipeline_Unbound_Constraints_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + TestSources.CreateTourWithHandler + """
            public sealed class QueryOnlyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IQuery<TResponse>
            {
                public ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.UnboundPipelineConstraints
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.QueryOnlyBehavior<TRequest, TResponse>", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Notification_Handlers_Without_Explicit_Order_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record TourCreated(int Id) : INotification;

            public sealed class HandlerA : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class HandlerB : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.TourCreated", StringComparison.Ordinal)
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.HandlerA", StringComparison.Ordinal));
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.HandlerB", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Duplicate_Notification_Order_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record TourCreated(int Id) : INotification;

            [NotificationOrder(10)]
            public sealed class HandlerA : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            [NotificationOrder(10)]
            public sealed class HandlerB : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.DuplicateNotificationHandlerOrder
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.TourCreated", StringComparison.Ordinal)
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("10", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Request_With_No_Handler_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingHandler
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.MissingTour", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Request_With_Multiple_Handlers_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandlerOne : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct) => ValueTask.FromResult("one");
            }

            public sealed class LookupTourHandlerTwo : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct) => ValueTask.FromResult("two");
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MultipleHandlers
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.LookupTour", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Unmarked_Module_Object_Dispatch_Coverage_Diagnostic()
    {
        // Arrange
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(TestSources.ModuleAUnmarkedSource, "SharedKernel.Mediator.Tests.ModuleA.Unmarked");
        var source = TestSources.DemoHeader + TestSources.CreateTourWithHandler;
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: [moduleReference]);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.UnprovenObjectDispatchCoverage
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains(
                                     "SharedKernel.Mediator.Tests.ModuleA.Unmarked",
                                     StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Explicit_Interface_Handler_Invalid_Signature_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class ExplicitLookupTourHandler : IQueryHandler<LookupTour, string>
            {
                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
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
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.ExplicitLookupTourHandler", StringComparison.Ordinal));
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingHandler
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.LookupTour", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::Demo.ExplicitLookupTourHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Explicit_Interface_Stream_Handler_Invalid_Signature_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record StreamTours() : IStreamRequest<string>;

            public sealed class ExplicitStreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                IAsyncEnumerable<string> IStreamRequestHandler<StreamTours, string>.Handle(StreamTours request, CancellationToken ct)
                {
                    yield return request.ToString();
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
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.ExplicitStreamToursHandler", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::Demo.ExplicitStreamToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Stream_Handler_Wrong_Return_Type_Invalid_Signature_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record StreamTours() : IStreamRequest<string>;

            public sealed class WrongReturnStreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public Task<IAsyncEnumerable<string>> Handle(StreamTours request, CancellationToken ct)
                    => Task.FromResult(AsyncEnumerable.Empty<string>());
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
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.WrongReturnStreamToursHandler", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::Demo.WrongReturnStreamToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Stream_Handler_Missing_CancellationToken_Invalid_Signature_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record StreamTours() : IStreamRequest<string>;

            public sealed class NoCancellationStreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request)
                {
                    yield return request.ToString();
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
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.NoCancellationStreamToursHandler", StringComparison.Ordinal));
        Assert.DoesNotContain("services.AddTransient<global::Demo.NoCancellationStreamToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_Missing_Handler_Diagnostic_Message()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;
        var diagnostic = Assert.Single(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingHandler);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);

        // Assert
        GeneratorSnapshotVerifier.Verify(message, extension: "txt");
    }

    [Fact]
    public void Snapshot_Duplicate_Pipeline_Order_Diagnostic_Message()
    {
        // Arrange
        const string source = TestSources.DemoHeader + TestSources.CreateTourWithHandler + """
            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationA : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }

            [PipelineOrder(PipelineStage.Validation, Order = 10)]
            public sealed class ValidationB : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;
        var diagnostic = diagnostics.First(static d => d.Id == MediatorDiagnosticIds.DuplicatePipelineOrder);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);

        // Assert
        Assert.Contains(
            diagnostics,
            static d => d.Id == MediatorDiagnosticIds.DuplicatePipelineOrder);
        GeneratorSnapshotVerifier.Verify(message, extension: "txt");
    }

    [Fact]
    public void Generate_Discovery_Report_Stream_Request_With_No_Handler_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record MissingStreamTour(int Count) : IStreamRequest<string>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingHandler
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.MissingStreamTour", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_Discovery_Report_Stream_Request_With_Multiple_Handlers_Diagnostic()
    {
        // Arrange
        const string source = TestSources.DemoHeader + """
            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandlerOne : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return "one";
                }
            }

            public sealed class StreamToursHandlerTwo : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return "two";
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var diagnostics = GeneratorTestHarness.RunGeneratorDriver(compilation).Results.Single().Diagnostics;

        // Assert
        Assert.Contains(
            diagnostics,
            static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MultipleHandlers
                                 && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("global::Demo.StreamTours", StringComparison.Ordinal));
    }

}
