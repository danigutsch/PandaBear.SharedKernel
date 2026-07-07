using System.Reflection;
using System.Runtime.CompilerServices;
using SharedKernel.Mediator;

namespace SharedKernel.Testing.Mediator;

/// <summary>
/// Collects explicit test registrations and builds a <see cref="ReferenceMediator"/>.
/// </summary>
public sealed class ReferenceDispatcherBuilder
{
    private static readonly MethodInfo CreateRequestHandlerRegistrationMethod =
        typeof(RegistrationFactory).GetMethod(
            nameof(RegistrationFactory.CreateRequestHandlerRegistrationCore),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo CreatePipelineRegistrationMethod =
        typeof(RegistrationFactory).GetMethod(
            nameof(RegistrationFactory.CreatePipelineRegistrationCore),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo CreateNotificationHandlerRegistrationMethod =
        typeof(RegistrationFactory).GetMethod(
            nameof(RegistrationFactory.CreateNotificationHandlerRegistrationCore),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo CreateStreamHandlerRegistrationMethod =
        typeof(RegistrationFactory).GetMethod(
            nameof(RegistrationFactory.CreateStreamHandlerRegistrationCore),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo CreateStreamPipelineRegistrationMethod =
        typeof(RegistrationFactory).GetMethod(
            nameof(RegistrationFactory.CreateStreamPipelineRegistrationCore),
            BindingFlags.Static | BindingFlags.Public)!;

    private readonly Dictionary<(Type RequestType, Type ResponseType), List<ReferenceMediator.RequestHandlerRegistration>> requestHandlers = [];
    private readonly Dictionary<(Type RequestType, Type ResponseType), List<ReferenceMediator.PipelineRegistration>> pipelines = [];
    private readonly Dictionary<Type, List<ReferenceMediator.NotificationHandlerRegistration>> notificationHandlers = [];
    private readonly Dictionary<(Type RequestType, Type ResponseType), List<ReferenceMediator.StreamHandlerRegistration>> streamHandlers = [];
    private readonly Dictionary<(Type RequestType, Type ResponseType), List<ReferenceMediator.StreamPipelineRegistration>> streamPipelines = [];

    private int pipelineRegistrationOrder;
    private int notificationRegistrationOrder;
    private int streamPipelineRegistrationOrder;

    /// <summary>
    /// Registers a typed request handler.
    /// </summary>
    /// <typeparam name="TRequest">The handled request type.</typeparam>
    /// <typeparam name="TResponse">The produced response type.</typeparam>
    /// <param name="handler">The handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddRequestHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handler);

        AddRequestHandlerCore(
            typeof(TRequest),
            typeof(TResponse),
            new ReferenceMediator.RequestHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                (request, ct) => BoxRequestHandler(handler, (TRequest)request, ct)));

        return this;
    }

    /// <summary>
    /// Registers a request handler using runtime types.
    /// </summary>
    /// <param name="requestType">The handled request type.</param>
    /// <param name="responseType">The produced response type.</param>
    /// <param name="handler">The handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddRequestHandler(Type requestType, Type responseType, object handler)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(handler);

        EnsureRequestType(requestType, responseType);
        EnsureHandlerType(typeof(IRequestHandler<,>), requestType, responseType, handler);

        AddRequestHandlerCore(
            requestType,
            responseType,
            CreateTypedRegistration<ReferenceMediator.RequestHandlerRegistration>(
                CreateRequestHandlerRegistrationMethod,
                requestType,
                responseType,
                handler));

        return this;
    }

    /// <summary>
    /// Registers a typed pipeline behavior.
    /// </summary>
    /// <typeparam name="TRequest">The request type handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The produced response type.</typeparam>
    /// <param name="pipeline">The pipeline instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddPipeline<TRequest, TResponse>(IPipelineBehavior<TRequest, TResponse> pipeline)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        AddPipelineCore(
            typeof(TRequest),
            typeof(TResponse),
            CreatePipelineRegistration(pipeline, pipelineRegistrationOrder++));

        return this;
    }

    /// <summary>
    /// Registers a pipeline behavior using runtime types.
    /// </summary>
    /// <param name="requestType">The request type handled by the pipeline.</param>
    /// <param name="responseType">The produced response type.</param>
    /// <param name="pipeline">The pipeline instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddPipeline(Type requestType, Type responseType, object pipeline)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(pipeline);

        EnsureRequestType(requestType, responseType);
        EnsureHandlerType(typeof(IPipelineBehavior<,>), requestType, responseType, pipeline);

        AddPipelineCore(
            requestType,
            responseType,
            CreateTypedRegistration<ReferenceMediator.PipelineRegistration>(
                CreatePipelineRegistrationMethod,
                requestType,
                responseType,
                pipeline,
                pipelineRegistrationOrder++));

        return this;
    }

    /// <summary>
    /// Registers a typed notification handler.
    /// </summary>
    /// <typeparam name="TNotification">The handled notification type.</typeparam>
    /// <param name="handler">The handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddNotificationHandler<TNotification>(INotificationHandler<TNotification> handler)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(handler);
        var notificationOrder = handler.GetType().GetCustomAttribute<NotificationOrderAttribute>();

        AddNotificationHandlerCore(
            typeof(TNotification),
            new ReferenceMediator.NotificationHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                notificationOrder?.Order ?? 0,
                notificationOrder is not null,
                notificationRegistrationOrder++,
                (notification, ct) => handler.Handle((TNotification)notification, ct)));

        return this;
    }

    /// <summary>
    /// Registers a notification handler using a runtime type.
    /// </summary>
    /// <param name="notificationType">The handled notification type.</param>
    /// <param name="handler">The handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddNotificationHandler(Type notificationType, object handler)
    {
        ArgumentNullException.ThrowIfNull(notificationType);
        ArgumentNullException.ThrowIfNull(handler);

        EnsureNotificationType(notificationType);
        EnsureHandlerType(typeof(INotificationHandler<>), notificationType, handler);

        AddNotificationHandlerCore(
            notificationType,
            CreateTypedRegistration<ReferenceMediator.NotificationHandlerRegistration>(
                CreateNotificationHandlerRegistrationMethod,
                notificationType,
                handler,
                notificationRegistrationOrder++));

        return this;
    }

    /// <summary>
    /// Registers a typed stream handler.
    /// </summary>
    /// <typeparam name="TRequest">The handled stream request type.</typeparam>
    /// <typeparam name="TResponse">The streamed item type.</typeparam>
    /// <param name="handler">The stream handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddStreamHandler<TRequest, TResponse>(IStreamRequestHandler<TRequest, TResponse> handler)
        where TRequest : IStreamRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handler);

        AddStreamHandlerCore(
            typeof(TRequest),
            typeof(TResponse),
            new ReferenceMediator.StreamHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                (request, ct) => BoxStreamHandler(handler, (TRequest)request, ct)));

        return this;
    }

    /// <summary>
    /// Registers a stream handler using runtime types.
    /// </summary>
    /// <param name="requestType">The handled stream request type.</param>
    /// <param name="responseType">The streamed item type.</param>
    /// <param name="handler">The stream handler instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddStreamHandler(Type requestType, Type responseType, object handler)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(handler);

        EnsureStreamRequestType(requestType, responseType);
        EnsureHandlerType(typeof(IStreamRequestHandler<,>), requestType, responseType, handler);

        AddStreamHandlerCore(
            requestType,
            responseType,
            CreateTypedRegistration<ReferenceMediator.StreamHandlerRegistration>(
                CreateStreamHandlerRegistrationMethod,
                requestType,
                responseType,
                handler));

        return this;
    }

    /// <summary>
    /// Registers a typed stream pipeline behavior.
    /// </summary>
    /// <typeparam name="TRequest">The stream request type handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The streamed item type.</typeparam>
    /// <param name="pipeline">The stream pipeline instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddStreamPipeline<TRequest, TResponse>(IStreamPipelineBehavior<TRequest, TResponse> pipeline)
        where TRequest : IStreamRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        AddStreamPipelineCore(
            typeof(TRequest),
            typeof(TResponse),
            CreateStreamPipelineRegistration(pipeline, streamPipelineRegistrationOrder++));

        return this;
    }

    /// <summary>
    /// Registers a stream pipeline behavior using runtime types.
    /// </summary>
    /// <param name="requestType">The stream request type handled by the pipeline.</param>
    /// <param name="responseType">The streamed item type.</param>
    /// <param name="pipeline">The stream pipeline instance to register.</param>
    /// <returns>The current builder.</returns>
    public ReferenceDispatcherBuilder AddStreamPipeline(Type requestType, Type responseType, object pipeline)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(pipeline);

        EnsureStreamRequestType(requestType, responseType);
        EnsureHandlerType(typeof(IStreamPipelineBehavior<,>), requestType, responseType, pipeline);

        AddStreamPipelineCore(
            requestType,
            responseType,
            CreateTypedRegistration<ReferenceMediator.StreamPipelineRegistration>(
                CreateStreamPipelineRegistrationMethod,
                requestType,
                responseType,
                pipeline,
                streamPipelineRegistrationOrder++));

        return this;
    }

    /// <summary>
    /// Builds the reference dispatcher from the registered handlers.
    /// </summary>
    /// <returns>The configured reference dispatcher.</returns>
    public ReferenceMediator Build()
    {
        var requestRoutes = new Dictionary<(Type RequestType, Type ResponseType), ReferenceMediator.RequestRoute>();
        foreach (var routeKey in requestHandlers.Keys.Union(pipelines.Keys))
        {
            var routeHandlers = requestHandlers.TryGetValue(routeKey, out var handlerRegistrations)
                ? handlerRegistrations
                : [];
            var routePipelines = pipelines.TryGetValue(routeKey, out var pipelineRegistrations)
                ? pipelineRegistrations
                    .OrderBy(static registration => registration.Stage)
                    .ThenBy(static registration => registration.Order)
                    .ThenBy(static registration => registration.RegistrationOrder)
                    .ThenBy(static registration => registration.ImplementationTypeName, StringComparer.Ordinal)
                    .ToArray()
                : [];

            requestRoutes[routeKey] = new ReferenceMediator.RequestRoute(routeHandlers.ToArray(), routePipelines);
        }

        var notificationRoutes = notificationHandlers.ToDictionary(
            static pair => pair.Key,
            static pair => new ReferenceMediator.NotificationRoute(
                [.. pair.Value
                    .OrderByDescending(static registration => registration.HasExplicitOrder)
                    .ThenBy(static registration => registration.Order)
                    .ThenBy(static registration => registration.RegistrationOrder)
                    .ThenBy(static registration => registration.ImplementationTypeName, StringComparer.Ordinal)],
                IsParallelNotificationDispatch(pair.Key)));

        var streamRoutes = new Dictionary<(Type RequestType, Type ResponseType), ReferenceMediator.StreamRoute>();
        foreach (var routeKey in streamHandlers.Keys.Union(streamPipelines.Keys))
        {
            var routeHandlers = streamHandlers.TryGetValue(routeKey, out var handlerRegistrations)
                ? handlerRegistrations
                : [];
            var routePipelines = streamPipelines.TryGetValue(routeKey, out var registrations)
                    ? registrations
                        .OrderBy(static registration => registration.Stage)
                        .ThenBy(static registration => registration.Order)
                        .ThenBy(static registration => registration.RegistrationOrder)
                        .ThenBy(static registration => registration.ImplementationTypeName, StringComparer.Ordinal)
                        .ToArray()
                    : [];
            streamRoutes[routeKey] = new ReferenceMediator.StreamRoute(routeHandlers.ToArray(), routePipelines);
        }

        return new ReferenceMediator(requestRoutes, notificationRoutes, streamRoutes);
    }

    private static async ValueTask<object?> BoxRequestHandler<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        return await handler.Handle(request, ct).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<object?> BoxStreamHandler<TRequest, TResponse>(
        IStreamRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        [EnumeratorCancellation] CancellationToken ct)
        where TRequest : IStreamRequest<TResponse>
    {
        await foreach (var item in handler.Handle(request, ct).WithCancellation(ct))
        {
            yield return item;
        }
    }

    private static ReferenceMediator.PipelineRegistration CreatePipelineRegistration<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse> pipeline,
        int registrationOrder)
        where TRequest : IRequest<TResponse>
    {
        var pipelineOrder = pipeline.GetType().GetCustomAttribute<PipelineOrderAttribute>();

        return new ReferenceMediator.PipelineRegistration(
            pipeline.GetType().FullName ?? pipeline.GetType().Name,
            pipelineOrder?.Stage ?? PipelineStage.Handler,
            pipelineOrder?.Order ?? 0,
            registrationOrder,
            (request, next, ct) => BoxPipeline(pipeline, (TRequest)request, next, ct));
    }

    private static async ValueTask<object?> BoxPipeline<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse> pipeline,
        TRequest request,
        ReferenceMediator.BoxedHandlerContinuation next,
        CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        var response = await pipeline.Handle(request, Next, ct).ConfigureAwait(false);
        return response;

        async ValueTask<TResponse> Next()
        {
            return ReferenceMediator.CastBoxedResult<TResponse>(await next().ConfigureAwait(false));
        }
    }

    private static ReferenceMediator.StreamPipelineRegistration CreateStreamPipelineRegistration<TRequest, TResponse>(
        IStreamPipelineBehavior<TRequest, TResponse> pipeline,
        int registrationOrder)
        where TRequest : IStreamRequest<TResponse>
    {
        var pipelineOrder = pipeline.GetType().GetCustomAttribute<PipelineOrderAttribute>();

        return new ReferenceMediator.StreamPipelineRegistration(
            pipeline.GetType().FullName ?? pipeline.GetType().Name,
            pipelineOrder?.Stage ?? PipelineStage.Handler,
            pipelineOrder?.Order ?? 0,
            registrationOrder,
            (request, next, ct) => BoxStreamPipeline(pipeline, (TRequest)request, next, ct));
    }

    private static async IAsyncEnumerable<object?> BoxStreamPipeline<TRequest, TResponse>(
        IStreamPipelineBehavior<TRequest, TResponse> pipeline,
        TRequest request,
        ReferenceMediator.BoxedStreamHandlerContinuation next,
        [EnumeratorCancellation] CancellationToken ct)
        where TRequest : IStreamRequest<TResponse>
    {
        await foreach (var item in pipeline.Handle(request, Next, ct).WithCancellation(ct))
        {
            yield return item;
        }

        async IAsyncEnumerable<TResponse> Next()
        {
            await foreach (var item in next().WithCancellation(ct))
            {
                yield return ReferenceMediator.CastBoxedResult<TResponse>(item);
            }
        }
    }

    private static TRegistration CreateTypedRegistration<TRegistration>(
        MethodInfo openMethod,
        Type requestType,
        Type responseType,
        object instance,
        params object[] extraArguments)
    {
        var closedMethod = openMethod.MakeGenericMethod(requestType, responseType);
        return (TRegistration)closedMethod.Invoke(null, [instance, .. extraArguments])!;
    }

    private static TRegistration CreateTypedRegistration<TRegistration>(
        MethodInfo openMethod,
        Type notificationType,
        object instance,
        params object[] extraArguments)
    {
        var closedMethod = openMethod.MakeGenericMethod(notificationType);
        return (TRegistration)closedMethod.Invoke(null, [instance, .. extraArguments])!;
    }

    private static void EnsureRequestType(Type requestType, Type responseType)
    {
        EnsureClosedGenericContract(requestType, typeof(IRequest<>), responseType, "requestType");
    }

    private static void EnsureStreamRequestType(Type requestType, Type responseType)
    {
        EnsureClosedGenericContract(requestType, typeof(IStreamRequest<>), responseType, "requestType");
    }

    private static void EnsureNotificationType(Type notificationType)
    {
        if (!typeof(INotification).IsAssignableFrom(notificationType))
        {
            throw new ArgumentException(
                $"Type '{notificationType.FullName}' must implement '{typeof(INotification).FullName}'.",
                nameof(notificationType));
        }
    }

    private static bool IsParallelNotificationDispatch(Type notificationType)
    {
        return notificationType.GetCustomAttribute<NotificationDispatchAttribute>()?.Strategy
               == NotificationDispatchStrategy.Parallel;
    }

    private static void EnsureClosedGenericContract(
        Type candidateType,
        Type openGenericContract,
        Type responseType,
        string parameterName)
    {
        var expectedContract = openGenericContract.MakeGenericType(responseType);
        if (!expectedContract.IsAssignableFrom(candidateType))
        {
            throw new ArgumentException(
                $"Type '{candidateType.FullName}' must implement '{expectedContract.FullName}'.",
                parameterName);
        }
    }

    private static void EnsureHandlerType(Type openGenericHandlerType, Type requestType, Type responseType, object handler)
    {
        var expectedHandlerType = openGenericHandlerType.MakeGenericType(requestType, responseType);
        if (!expectedHandlerType.IsInstanceOfType(handler))
        {
            throw new ArgumentException(
                $"Handler type '{handler.GetType().FullName}' must implement '{expectedHandlerType.FullName}'.",
                nameof(handler));
        }
    }

    private static void EnsureHandlerType(Type openGenericHandlerType, Type notificationType, object handler)
    {
        var expectedHandlerType = openGenericHandlerType.MakeGenericType(notificationType);
        if (!expectedHandlerType.IsInstanceOfType(handler))
        {
            throw new ArgumentException(
                $"Handler type '{handler.GetType().FullName}' must implement '{expectedHandlerType.FullName}'.",
                nameof(handler));
        }
    }

    private void AddRequestHandlerCore(
        Type requestType,
        Type responseType,
        ReferenceMediator.RequestHandlerRegistration registration)
    {
        var routeKey = (requestType, responseType);
        if (!requestHandlers.TryGetValue(routeKey, out var registrations))
        {
            registrations = [];
            requestHandlers[routeKey] = registrations;
        }

        registrations.Add(registration);
    }

    private void AddPipelineCore(
        Type requestType,
        Type responseType,
        ReferenceMediator.PipelineRegistration registration)
    {
        var routeKey = (requestType, responseType);
        if (!pipelines.TryGetValue(routeKey, out var registrations))
        {
            registrations = [];
            pipelines[routeKey] = registrations;
        }

        registrations.Add(registration);
    }

    private void AddNotificationHandlerCore(Type notificationType, ReferenceMediator.NotificationHandlerRegistration registration)
    {
        if (!notificationHandlers.TryGetValue(notificationType, out var registrations))
        {
            registrations = [];
            notificationHandlers[notificationType] = registrations;
        }

        registrations.Add(registration);
    }

    private void AddStreamHandlerCore(
        Type requestType,
        Type responseType,
        ReferenceMediator.StreamHandlerRegistration registration)
    {
        var routeKey = (requestType, responseType);
        if (!streamHandlers.TryGetValue(routeKey, out var registrations))
        {
            registrations = [];
            streamHandlers[routeKey] = registrations;
        }

        registrations.Add(registration);
    }

    private void AddStreamPipelineCore(
        Type requestType,
        Type responseType,
        ReferenceMediator.StreamPipelineRegistration registration)
    {
        var routeKey = (requestType, responseType);
        if (!streamPipelines.TryGetValue(routeKey, out var registrations))
        {
            registrations = [];
            streamPipelines[routeKey] = registrations;
        }

        registrations.Add(registration);
    }

    private static class RegistrationFactory
    {
        public static ReferenceMediator.RequestHandlerRegistration CreateRequestHandlerRegistrationCore<TRequest, TResponse>(
            object handler)
            where TRequest : IRequest<TResponse>
        {
            return new ReferenceMediator.RequestHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                (request, ct) => BoxRequestHandler((IRequestHandler<TRequest, TResponse>)handler, (TRequest)request, ct));
        }

        public static ReferenceMediator.PipelineRegistration CreatePipelineRegistrationCore<TRequest, TResponse>(
            object pipeline,
            int registrationOrder)
            where TRequest : IRequest<TResponse>
        {
            return CreatePipelineRegistration((IPipelineBehavior<TRequest, TResponse>)pipeline, registrationOrder);
        }

        public static ReferenceMediator.NotificationHandlerRegistration CreateNotificationHandlerRegistrationCore<TNotification>(
            object handler,
            int registrationOrder)
            where TNotification : INotification
        {
            var notificationOrder = handler.GetType().GetCustomAttribute<NotificationOrderAttribute>();
            return new ReferenceMediator.NotificationHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                notificationOrder?.Order ?? 0,
                notificationOrder is not null,
                registrationOrder,
                (notification, ct) => ((INotificationHandler<TNotification>)handler).Handle((TNotification)notification, ct));
        }

        public static ReferenceMediator.StreamHandlerRegistration CreateStreamHandlerRegistrationCore<TRequest, TResponse>(
            object handler)
            where TRequest : IStreamRequest<TResponse>
        {
            return new ReferenceMediator.StreamHandlerRegistration(
                handler.GetType().FullName ?? handler.GetType().Name,
                (request, ct) => BoxStreamHandler((IStreamRequestHandler<TRequest, TResponse>)handler, (TRequest)request, ct));
        }

        public static ReferenceMediator.StreamPipelineRegistration CreateStreamPipelineRegistrationCore<TRequest, TResponse>(
            object pipeline,
            int registrationOrder)
            where TRequest : IStreamRequest<TResponse>
        {
            return CreateStreamPipelineRegistration((IStreamPipelineBehavior<TRequest, TResponse>)pipeline, registrationOrder);
        }
    }
}
