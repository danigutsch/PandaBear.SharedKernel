using SharedKernel.Mediator.Testing.ReferenceDispatcher;

namespace SharedKernel.Mediator.GeneratorTests;

internal sealed class StreamDispatchTestScenario : IDisposable
{
    private StreamDispatchTestScenario(
        GeneratedMediatorRuntimeContext runtime,
        IStreamRequest<string> request,
        ReferenceMediator referenceDispatcher,
        IMediator mediator)
    {
        Runtime = runtime;
        Request = request;
        ReferenceDispatcher = referenceDispatcher;
        Mediator = mediator;
    }

    public GeneratedMediatorRuntimeContext Runtime { get; }

    public IStreamRequest<string> Request { get; }

    public ReferenceMediator ReferenceDispatcher { get; }

    public IMediator Mediator { get; }

    public static StreamDispatchTestScenario Create(string source, int requestCount)
    {
        var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.StreamTours");
        var handler = runtime.CreateInstance("Demo.StreamToursHandler");
        var validation = runtime.CreateInstance("Demo.ValidationBehavior");
        var observability = runtime.CreateInstance("Demo.ObservabilityBehavior");
        var request = runtime.CreateInstance<IStreamRequest<string>>("Demo.StreamTours", requestCount);
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(requestType, typeof(string), handler)
            .AddStreamPipeline(requestType, typeof(string), validation)
            .AddStreamPipeline(requestType, typeof(string), observability)
            .Build();
        var mediator = runtime.CreateMediator(handler, validation, observability);

        return new StreamDispatchTestScenario(runtime, request, referenceDispatcher, mediator);
    }

    public void Dispose()
    {
        Runtime.Dispose();
    }

    public string[] ReadTrace()
    {
        return Runtime.ReadTraceEntries("Demo.TraceLog");
    }

    public void ClearTrace()
    {
        Runtime.ClearTraceEntries("Demo.TraceLog");
    }

    public IAsyncEnumerable<string> SendReference(CancellationToken cancellationToken = default)
    {
        return ReferenceDispatcher.Send(Request, cancellationToken);
    }

    public IAsyncEnumerable<string> SendGenerated(CancellationToken cancellationToken = default)
    {
        return Mediator.Send(Request, cancellationToken);
    }
}
