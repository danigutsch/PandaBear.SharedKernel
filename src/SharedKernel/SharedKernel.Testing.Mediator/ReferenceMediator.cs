using System.Runtime.CompilerServices;
using SharedKernel.Mediator;

namespace SharedKernel.Testing.Mediator;

/// <summary>
/// Provides a test-only mediator implementation used as a correctness oracle for generated dispatch.
/// </summary>
public sealed class ReferenceMediator : IMediator
{
    private readonly Dictionary<(Type RequestType, Type ResponseType), RequestRoute> requestRoutes;
    private readonly Dictionary<Type, NotificationRoute> notificationRoutes;
    private readonly Dictionary<(Type RequestType, Type ResponseType), StreamRoute> streamRoutes;

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

    internal delegate ValueTask<object?> BoxedHandlerContinuation();

    internal delegate IAsyncEnumerable<object?> BoxedStreamHandlerContinuation();

    internal sealed class RequestRoute(
        IReadOnlyList<RequestHandlerRegistration> registrations,
        IReadOnlyList<PipelineRegistration> orderedPipelines)
    {
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

    internal sealed class NotificationRoute(
        IReadOnlyList<NotificationHandlerRegistration> handlers,
        bool publishInParallel)
    {
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

    internal sealed class StreamRoute(
        IReadOnlyList<StreamHandlerRegistration> registrations,
        IReadOnlyList<StreamPipelineRegistration> pipelines)
    {
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

    internal sealed class RequestHandlerRegistration(
        string implementationTypeName,
        Func<object, CancellationToken, ValueTask<object?>> invoke)
    {
        public string ImplementationTypeName { get; } = implementationTypeName;

        public Func<object, CancellationToken, ValueTask<object?>> Invoke { get; } = invoke;
    }

    internal sealed class PipelineRegistration(
        string implementationTypeName,
        PipelineStage stage,
        int order,
        int registrationOrder,
        Func<object, BoxedHandlerContinuation, CancellationToken, ValueTask<object?>> invoke)
    {
        public string ImplementationTypeName { get; } = implementationTypeName;

        public PipelineStage Stage { get; } = stage;

        public int Order { get; } = order;

        public int RegistrationOrder { get; } = registrationOrder;

        public Func<object, BoxedHandlerContinuation, CancellationToken, ValueTask<object?>> Invoke { get; } = invoke;
    }

    internal sealed class NotificationHandlerRegistration(
        string implementationTypeName,
        int order,
        bool hasExplicitOrder,
        int registrationOrder,
        Func<object, CancellationToken, ValueTask> invoke)
    {
        public string ImplementationTypeName { get; } = implementationTypeName;

        public int Order { get; } = order;

        public bool HasExplicitOrder { get; } = hasExplicitOrder;

        public int RegistrationOrder { get; } = registrationOrder;

        public Func<object, CancellationToken, ValueTask> Invoke { get; } = invoke;
    }

    internal sealed class StreamHandlerRegistration(
        string implementationTypeName,
        Func<object, CancellationToken, IAsyncEnumerable<object?>> invoke)
    {
        public string ImplementationTypeName { get; } = implementationTypeName;

        public Func<object, CancellationToken, IAsyncEnumerable<object?>> Invoke { get; } = invoke;
    }

    internal sealed class StreamPipelineRegistration(
        string implementationTypeName,
        PipelineStage stage,
        int order,
        int registrationOrder,
        Func<object, BoxedStreamHandlerContinuation, CancellationToken, IAsyncEnumerable<object?>> invoke)
    {
        public string ImplementationTypeName { get; } = implementationTypeName;

        public PipelineStage Stage { get; } = stage;

        public int Order { get; } = order;

        public int RegistrationOrder { get; } = registrationOrder;

        public Func<object, BoxedStreamHandlerContinuation, CancellationToken, IAsyncEnumerable<object?>> Invoke { get; } = invoke;
    }

    private sealed class ThrowingAsyncEnumerable<T>(Exception exception) : IAsyncEnumerable<T>, IAsyncEnumerator<T>
    {
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
