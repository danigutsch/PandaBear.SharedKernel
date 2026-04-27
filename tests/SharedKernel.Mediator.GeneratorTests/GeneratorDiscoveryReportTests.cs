namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiscoveryReportTests
{
    [Fact]
    public void Generate_Discovery_Report_Single_Project_Counts()
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
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("RequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("HandlerCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("PipelineCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("NotificationCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.Contains("StreamRequestCount = 1;", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Repeated_Runs_Are_Deterministic()
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

    [Fact]
    public void Generate_Discovery_Report_Marked_Module_Assembly_Included()
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
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

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
        const string moduleSource = """
            using SharedKernel.Mediator;

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            public sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        var moduleReference = GeneratorTestHarness.CreateMetadataReference(moduleSource, "SharedKernel.Mediator.Tests.ModuleA.Unmarked");
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
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains("RequestCount = 1;", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedKernel.Mediator.Tests.ModuleA.Unmarked", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::ModuleA.SearchTours", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Response_Metadata_Captured()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record TourRow(int Id, string Name);

            public sealed record Page<TItem>(IReadOnlyList<TItem> Items);

            public sealed record ListTours() : IQuery<Page<TourRow>>;

            public sealed class ListToursHandler : IQueryHandler<ListTours, Page<TourRow>>
            {
                public ValueTask<Page<TourRow>> Handle(ListTours request, CancellationToken ct) => ValueTask.FromResult(new Page<TourRow>([]));
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains("global::Demo.ListTours | Kind=Query", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Response=global::Demo.Page<global::Demo.TourRow>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseGenericDefinition=global::Demo.Page<TItem>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResponseTypeArguments=[global::Demo.TourRow]", generatedSource, StringComparison.Ordinal);
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
        var compilation = GeneratorTestHarness.CreateCompilation(source, includeMediatorReference: false);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

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
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

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
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains("global::Demo.Container.LookupTour | Kind=Request", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.Container.LookupTourHandler(Request,Public,Handle)", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Order=20", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Command_Without_Response_Uses_Unit()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record DeleteTour(int Id) : ICommand;

            public sealed class DeleteTourHandler : ICommandHandler<DeleteTour>
            {
                public ValueTask<Unit> Handle(DeleteTour request, CancellationToken ct) => ValueTask.FromResult(Unit.Value);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

        // Assert
        Assert.Contains("global::Demo.DeleteTour | Kind=Command | Response=global::SharedKernel.Mediator.Unit", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.DeleteTourHandler(Command,Public,Handle)", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Discovery_Report_Abstract_And_Interface_Shapes_Are_Ignored()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

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
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(compilation);

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
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

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
        AssertOrdered(
            generatedSource,
            "global::Demo.AlphaCreateTourHandler(CommandWithResponse,Public,Handle)",
            "global::Demo.ZedCreateTourHandler(CommandWithResponse,Public,Handle)");
        AssertOrdered(
            generatedSource,
            "global::Demo.ValidationEarlyBehavior(Stage=-1000,Order=5,Applicability=Closed,OpenGeneric=<none>)",
            "global::Demo.ValidationLateBehavior(Stage=-1000,Order=30,Applicability=Closed,OpenGeneric=<none>)",
            "global::Demo.TransactionBehavior(Stage=-400,Order=10,Applicability=Closed,OpenGeneric=<none>)");
    }

    private static void AssertOrdered(string actual, params string[] expectedSegments)
    {
        var currentIndex = -1;

        foreach (var expectedSegment in expectedSegments)
        {
            var nextIndex = actual.IndexOf(expectedSegment, currentIndex + 1, StringComparison.Ordinal);
            Assert.True(nextIndex >= 0, $"Expected segment not found: {expectedSegment}");
            Assert.True(nextIndex > currentIndex, $"Expected segment out of order: {expectedSegment}");
            currentIndex = nextIndex;
        }
    }
}
