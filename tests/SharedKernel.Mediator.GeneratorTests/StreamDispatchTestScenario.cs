using PandaBear.Testing.Mediator;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Holds a compiled stream request scenario with its generated mediator and a reference dispatcher so tests can compare both dispatch paths.</summary>
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

    /// <summary>The runtime context that compiled the scenario source.</summary>
    public GeneratedMediatorRuntimeContext Runtime
    {
        get;
    }

    /// <summary>The StreamTours request instance sent through both dispatchers.</summary>
    public IStreamRequest<string> Request
    {
        get;
    }

    /// <summary>The reference mediator wired with the same handler and pipeline behaviors.</summary>
    public ReferenceMediator ReferenceDispatcher
    {
        get;
    }

    /// <summary>The generated mediator built from the same handler and pipeline behaviors.</summary>
    public IMediator Mediator
    {
        get;
    }

    /// <summary>Compiles the source and builds a Demo.StreamTours request with the given count, plus a generated mediator and a reference dispatcher that share one handler, validation behavior and observability behavior.</summary>
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

    /// <inheritdoc />
    public void Dispose()
    {
        Runtime.Dispose();
    }

    /// <summary>Returns the entries recorded in Demo.TraceLog.</summary>
    public string[] ReadTrace()
    {
        return Runtime.ReadTraceEntries("Demo.TraceLog");
    }

    /// <summary>Empties Demo.TraceLog.</summary>
    public void ClearTrace()
    {
        Runtime.ClearTraceEntries("Demo.TraceLog");
    }

    /// <summary>Streams the request through the reference dispatcher.</summary>
    public IAsyncEnumerable<string> SendReference(CancellationToken cancellationToken = default)
    {
        return ReferenceDispatcher.Send(Request, cancellationToken);
    }

    /// <summary>Streams the request through the generated mediator.</summary>
    public IAsyncEnumerable<string> SendGenerated(CancellationToken cancellationToken = default)
    {
        return Mediator.Send(Request, cancellationToken);
    }
}
