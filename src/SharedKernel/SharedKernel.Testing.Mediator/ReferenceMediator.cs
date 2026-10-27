using System.Runtime.CompilerServices;
using PandaBear.Mediator;

namespace PandaBear.Testing.Mediator;

/// <summary>
/// Provides a test-only mediator implementation used as a correctness oracle for generated dispatch.
/// </summary>
public sealed class ReferenceMediator : IMediator
{
    private readonly Dictionary<(Type RequestType, Type ResponseType), RequestRoute> requestRoutes;
    private readonly Dictionary<Type, NotificationRoute> notificationRoutes;
    private readonly Dictionary<(Type RequestType, Type ResponseType), StreamRoute> streamRoutes;

    /// <summary>
    /// Creates a mediator over precomputed routes.
    /// </summary>
    /// <param name="requestRoutes">Request routes keyed by request and response type.</param>
    /// <param name="notificationRoutes">Notification routes keyed by notification type.</param>
    /// <param name="streamRoutes">Stream routes keyed by request and response type.</param>
    internal ReferenceMediator(
        IDictionary<(Type RequestType, Type ResponseType), RequestRoute> requestRoutes,
        IDictionary<Type, NotificationRoute> notificationRoutes,
        IDictionary<(Type RequestType, Type ResponseType), StreamRoute> streamRoutes)
    {
        this.requestRoutes = new Dictionary<(Type RequestType, Type ResponseType), RequestRoute>(requestRoutes);
        this.notificationRoutes = new Dictionary<Type, NotificationRoute>(notificationRoutes);
        this.streamRoutes = new Dictionary<(Type RequestType, Type ResponseType), StreamRoute>(streamRoutes);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!requestRoutes.TryGetValue((request.GetType(), typeof(TResponse)), out var route))
        {
            throw new NotSupportedException(
                $"Reference request dispatch is not available for request type '{request.GetType().FullName}'.");
        }

        return route.Invoke(request, ct);
    }

    /// <summary>
    /// Executes a registered stream handler for the provided request.
    /// </summary>
    /// <typeparam name="TResponse">The streamed item type.</typeparam>
    /// <param name="request">The stream request instance to dispatch.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The produced stream of response items.</returns>
    public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!streamRoutes.TryGetValue((request.GetType(), typeof(TResponse)), out var route))
        {
            return new ThrowingAsyncEnumerable<TResponse>(
                new NotSupportedException(
                    $"Reference stream dispatch is not available for request type '{request.GetType().FullName}'."));
        }

        return route.Invoke(request, ct);
    }

    /// <inheritdoc />
    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!notificationRoutes.TryGetValue(notification.GetType(), out var route))
        {
            return ValueTask.CompletedTask;
        }

        return route.Publish(notification, ct);
    }

    /// <summary>Returns the boxed handler result as <typeparamref name="TResponse" />, or throws when its runtime type does not match.</summary>
    internal static TResponse CastBoxedResult<TResponse>(object? result)
    {
        if (result is TResponse typed)
        {
            return typed;
        }

        var sourceType = result?.GetType() ?? typeof(void);
        throw new InvalidOperationException(
            $"Reference request dispatch returned '{sourceType.FullName}' when '{typeof(TResponse).FullName}' was expected.");
    }

    /// <summary>Continues a boxed request pipeline with the next behavior or the handler.</summary>
    internal delegate ValueTask<object?> BoxedHandlerContinuation();

    /// <summary>Continues a boxed stream pipeline with the next behavior or the handler.</summary>
    internal delegate IAsyncEnumerable<object?> BoxedStreamHandlerContinuation();

    /// <summary>
    /// Runs the pipelines and the single handler for one request type.
    /// </summary>
    /// <param name="registrations">Handlers registered for the request type.</param>
    /// <param name="orderedPipelines">Pipeline behaviors in execution order.</param>
    internal sealed class RequestRoute(
        IReadOnlyList<RequestHandlerRegistration> registrations,
        IReadOnlyList<PipelineRegistration> orderedPipelines)
    {
        /// <summary>Dispatches the request through the pipelines to its handler, and throws when there is not exactly one handler.</summary>
        public ValueTask<TResponse> Invoke<TResponse>(IRequest<TResponse> request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return registrations.Count switch
            {
                0 => ThrowNoHandler(request),
                1 => InvokeCore(request, ct),
                _ => ThrowAmbiguousHandlers(request, registrations.Count),
            };
        }

        private async ValueTask<TResponse> InvokeCore<TResponse>(IRequest<TResponse> request, CancellationToken ct)
        {
            BoxedHandlerContinuation next = () => registrations[0].Invoke(request, ct);

            for (var index = orderedPipelines.Count - 1; index >= 0; index--)
            {
                var current = orderedPipelines[index];
                var currentNext = next;
                next = () => current.Invoke(request, currentNext, ct);
            }

            return CastBoxedResult<TResponse>(await next().ConfigureAwait(false));
        }

        private static ValueTask<TResponse> ThrowNoHandler<TResponse>(IRequest<TResponse> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            throw new NotSupportedException(
                $"Reference request dispatch is not available for request type '{request.GetType().FullName}'.");
        }

        private static ValueTask<TResponse> ThrowAmbiguousHandlers<TResponse>(IRequest<TResponse> request, int handlerCount)
        {
            ArgumentNullException.ThrowIfNull(request);

            throw new InvalidOperationException(
                $"Reference request dispatch found {handlerCount} registered handlers for request type '{request.GetType().FullName}'.");
        }
    }

    /// <summary>
    /// Runs all handlers for one notification type.
    /// </summary>
    /// <param name="handlers">Handlers in execution order.</param>
    /// <param name="publishInParallel">Whether handlers run concurrently instead of one after another.</param>
    internal sealed class NotificationRoute(
        IReadOnlyList<NotificationHandlerRegistration> handlers,
        bool publishInParallel)
    {
        /// <summary>Delivers the notification to every handler, concurrently or in order.</summary>
        public async ValueTask Publish(object notification, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (publishInParallel && handlers.Count > 1)
            {
                await Task.WhenAll(handlers.Select(handler => handler.Invoke(notification, ct).AsTask())).ConfigureAwait(false);
                return;
            }

            foreach (var handler in handlers)
            {
                await handler.Invoke(notification, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs the pipelines and the single handler for one stream request type.
    /// </summary>
    /// <param name="registrations">Handlers registered for the request type.</param>
    /// <param name="pipelines">Pipeline behaviors in execution order.</param>
    internal sealed class StreamRoute(
        IReadOnlyList<StreamHandlerRegistration> registrations,
        IReadOnlyList<StreamPipelineRegistration> pipelines)
    {
        /// <summary>Dispatches the stream request through the pipelines to its handler, and throws when there is not exactly one handler.</summary>
        public IAsyncEnumerable<TResponse> Invoke<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return registrations.Count switch
            {
                0 => new ThrowingAsyncEnumerable<TResponse>(
                    new NotSupportedException(
                        $"Reference stream dispatch is not available for request type '{request.GetType().FullName}'.")),
                1 => InvokeCore(request, ct),
                _ => new ThrowingAsyncEnumerable<TResponse>(
                    new InvalidOperationException(
                        $"Reference stream dispatch found {registrations.Count} registered handlers for request type '{request.GetType().FullName}'.")),
            };
        }

        private IAsyncEnumerable<TResponse> InvokeCore<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
        {
            BoxedStreamHandlerContinuation next = () => registrations[0].Invoke(request, ct);

            for (var index = pipelines.Count - 1; index >= 0; index--)
            {
                var current = pipelines[index];
                var currentNext = next;
                next = () => current.Invoke(request, currentNext, ct);
            }

            return CastStream<TResponse>(next(), ct);
        }

        private static async IAsyncEnumerable<TResponse> CastStream<TResponse>(
            IAsyncEnumerable<object?> source,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var item in source.WithCancellation(ct))
            {
                yield return CastBoxedResult<TResponse>(item);
            }
        }

    }

    /// <summary>
    /// A request handler registered with the reference mediator.
    /// </summary>
    /// <param name="implementationTypeName">Name of the implementation type, used in diagnostics.</param>
    /// <param name="invoke">Delegate that calls the handler.</param>
    internal sealed class RequestHandlerRegistration(
        string implementationTypeName,
        Func<object, CancellationToken, ValueTask<object?>> invoke)
    {
        /// <summary>Name of the registered implementation type, used in diagnostics.</summary>
        public string ImplementationTypeName { get; } = implementationTypeName;

        /// <summary>Calls the handler with a boxed request and returns the boxed response.</summary>
        public Func<object, CancellationToken, ValueTask<object?>> Invoke { get; } = invoke;
    }

    /// <summary>
    /// A request pipeline behavior registered with the reference mediator.
    /// </summary>
    /// <param name="implementationTypeName">Name of the implementation type, used in diagnostics.</param>
    /// <param name="stage">Pipeline stage the behavior runs in.</param>
    /// <param name="order">Explicit order within the stage.</param>
    /// <param name="registrationOrder">Position at which the registration was added.</param>
    /// <param name="invoke">Delegate that runs the behavior.</param>
    internal sealed class PipelineRegistration(
        string implementationTypeName,
        PipelineStage stage,
        int order,
        int registrationOrder,
        Func<object, BoxedHandlerContinuation, CancellationToken, ValueTask<object?>> invoke)
    {
        /// <summary>Name of the registered implementation type, used in diagnostics.</summary>
        public string ImplementationTypeName { get; } = implementationTypeName;

        /// <summary>Pipeline stage the behavior runs in.</summary>
        public PipelineStage Stage { get; } = stage;

        /// <summary>Explicit order within the stage; lower values run first.</summary>
        public int Order { get; } = order;

        /// <summary>Position at which the registration was added; breaks ties between equal orders.</summary>
        public int RegistrationOrder { get; } = registrationOrder;

        /// <summary>Runs the behavior around the next continuation with a boxed request and returns the boxed response.</summary>
        public Func<object, BoxedHandlerContinuation, CancellationToken, ValueTask<object?>> Invoke { get; } = invoke;
    }

    /// <summary>
    /// A notification handler registered with the reference mediator.
    /// </summary>
    /// <param name="implementationTypeName">Name of the implementation type, used in diagnostics.</param>
    /// <param name="order">Explicit order; lower values run first.</param>
    /// <param name="hasExplicitOrder">Whether the order was set explicitly rather than defaulted.</param>
    /// <param name="registrationOrder">Position at which the registration was added.</param>
    /// <param name="invoke">Delegate that calls the handler.</param>
    internal sealed class NotificationHandlerRegistration(
        string implementationTypeName,
        int order,
        bool hasExplicitOrder,
        int registrationOrder,
        Func<object, CancellationToken, ValueTask> invoke)
    {
        /// <summary>Name of the registered implementation type, used in diagnostics.</summary>
        public string ImplementationTypeName { get; } = implementationTypeName;

        /// <summary>Explicit order within the stage; lower values run first.</summary>
        public int Order { get; } = order;

        /// <summary>Whether the order was set explicitly rather than defaulted; explicitly ordered handlers run first.</summary>
        public bool HasExplicitOrder { get; } = hasExplicitOrder;

        /// <summary>Position at which the registration was added; breaks ties between equal orders.</summary>
        public int RegistrationOrder { get; } = registrationOrder;

        /// <summary>Calls the handler with a boxed notification.</summary>
        public Func<object, CancellationToken, ValueTask> Invoke { get; } = invoke;
    }

    /// <summary>
    /// A stream handler registered with the reference mediator.
    /// </summary>
    /// <param name="implementationTypeName">Name of the implementation type, used in diagnostics.</param>
    /// <param name="invoke">Delegate that calls the handler.</param>
    internal sealed class StreamHandlerRegistration(
        string implementationTypeName,
        Func<object, CancellationToken, IAsyncEnumerable<object?>> invoke)
    {
        /// <summary>Name of the registered implementation type, used in diagnostics.</summary>
        public string ImplementationTypeName { get; } = implementationTypeName;

        /// <summary>Calls the handler with a boxed stream request and yields boxed items.</summary>
        public Func<object, CancellationToken, IAsyncEnumerable<object?>> Invoke { get; } = invoke;
    }

    /// <summary>
    /// A stream pipeline behavior registered with the reference mediator.
    /// </summary>
    /// <param name="implementationTypeName">Name of the implementation type, used in diagnostics.</param>
    /// <param name="stage">Pipeline stage the behavior runs in.</param>
    /// <param name="order">Explicit order within the stage.</param>
    /// <param name="registrationOrder">Position at which the registration was added.</param>
    /// <param name="invoke">Delegate that runs the behavior.</param>
    internal sealed class StreamPipelineRegistration(
        string implementationTypeName,
        PipelineStage stage,
        int order,
        int registrationOrder,
        Func<object, BoxedStreamHandlerContinuation, CancellationToken, IAsyncEnumerable<object?>> invoke)
    {
        /// <summary>Name of the registered implementation type, used in diagnostics.</summary>
        public string ImplementationTypeName { get; } = implementationTypeName;

        /// <summary>Pipeline stage the behavior runs in.</summary>
        public PipelineStage Stage { get; } = stage;

        /// <summary>Explicit order within the stage; lower values run first.</summary>
        public int Order { get; } = order;

        /// <summary>Position at which the registration was added; breaks ties between equal orders.</summary>
        public int RegistrationOrder { get; } = registrationOrder;

        /// <summary>Runs the behavior around the next stream continuation and yields boxed items.</summary>
        public Func<object, BoxedStreamHandlerContinuation, CancellationToken, IAsyncEnumerable<object?>> Invoke { get; } = invoke;
    }

    private sealed class ThrowingAsyncEnumerable<T>(Exception exception) : IAsyncEnumerable<T>, IAsyncEnumerator<T>
    {
        /// <summary>Always throws the captured exception.</summary>
        public T Current => throw exception;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromException<bool>(exception);
        }
    }
}
