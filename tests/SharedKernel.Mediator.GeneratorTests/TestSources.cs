namespace SharedKernel.Mediator.GeneratorTests;

/// <summary>
/// Composable source string building blocks for generator tests.
/// Concatenate constants to build minimal-but-complete compilation inputs.
/// </summary>
internal static class TestSources
{
    /// <summary>
    /// Standard primary-assembly header: mediator using, module marker, Demo namespace.
    /// </summary>
    public const string ModuleHeader = """
        using SharedKernel.Mediator;

        [assembly: MediatorModule]

        namespace Demo;

        """;

    /// <summary>
    /// Demo namespace header without the module marker (for secondary-assembly primary sources).
    /// </summary>
    public const string DemoHeader = """
        using SharedKernel.Mediator;

        namespace Demo;

        """;

    /// <summary>
    /// <c>CreateTour</c> ICommand&lt;int&gt; record plus its closed handler returning 42.
    /// </summary>
    public const string CreateTourWithHandler = """
        public sealed record CreateTour(string Name) : ICommand<int>;

        public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
        {
            public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
        }

        """;

    /// <summary>
    /// Closed <c>ValidationBehavior</c> for <c>CreateTour/int</c> at the Validation stage with no explicit order.
    /// </summary>
    public const string CreateTourValidationBehavior = """
        [PipelineOrder(PipelineStage.Validation)]
        public sealed class ValidationBehavior : IPipelineBehavior<CreateTour, int>
        {
            public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
        }

        """;

    /// <summary>
    /// <c>TourCreated</c> INotification record plus its sequential handler.
    /// </summary>
    public const string TourCreatedWithHandler = """
        public sealed record TourCreated(int Id) : INotification;

        public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
        {
            public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
        }

        """;

    /// <summary>
    /// <c>StreamTours</c> IStreamRequest&lt;string&gt; record plus its handler.
    /// </summary>
    public const string StreamToursWithHandler = """
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

    /// <summary>
    /// <c>GetTour</c> IQuery&lt;string&gt; record plus its handler returning "tour".
    /// </summary>
    public const string GetTourWithHandler = """
        public sealed record GetTour(int Id) : IQuery<string>;

        public sealed class GetTourHandler : IQueryHandler<GetTour, string>
        {
            public ValueTask<string> Handle(GetTour request, CancellationToken ct) => ValueTask.FromResult("tour");
        }

        """;

    /// <summary>
    /// <c>TourCreated</c> INotification with <c>Parallel</c> dispatch strategy plus its single handler.
    /// </summary>
    public const string TourCreatedParallelWithHandler = """
        [NotificationDispatch(NotificationDispatchStrategy.Parallel)]
        public sealed record TourCreated(int Id) : INotification;

        public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
        {
            public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
        }

        """;

    /// <summary>
    /// Marked ModuleA assembly source: module marker, SearchTours query, and public handler.
    /// Pass as the source argument to <see cref="GeneratorTestHarness.CreateMetadataReference"/>.
    /// </summary>
    public const string ModuleAMarkedSource = """
        using SharedKernel.Mediator;

        [assembly: MediatorModule]

        namespace ModuleA;

        public sealed record SearchTours(string Query) : IQuery<int>;

        public sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
        {
            public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
        }

        """;

    /// <summary>
    /// Unmarked ModuleA assembly source: no module marker, SearchTours query, and public handler.
    /// Pass as the source argument to <see cref="GeneratorTestHarness.CreateMetadataReference"/>.
    /// </summary>
    public const string ModuleAUnmarkedSource = """
        using SharedKernel.Mediator;

        namespace ModuleA;

        public sealed record SearchTours(string Query) : IQuery<int>;

        public sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
        {
            public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
        }

        """;
}
