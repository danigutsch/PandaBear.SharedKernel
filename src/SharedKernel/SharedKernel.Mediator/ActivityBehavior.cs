namespace SharedKernel.Mediator;

/// <summary>
/// Adds optional activity-based observability around mediator request dispatch.
/// </summary>
/// <typeparam name="TRequest">The mediator request type.</typeparam>
/// <typeparam name="TResponse">The mediator response type.</typeparam>
public sealed class ActivityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        using var activity = SharedKernelMediatorActivitySource.StartRequestActivity<TRequest>();
        if (activity is not null)
        {
            activity.SetTag("sharedkernel.mediator.request_type", request.GetType().FullName ?? typeof(TRequest).FullName ?? typeof(TRequest).Name);
            activity.SetTag("sharedkernel.mediator.response_type", typeof(TResponse).FullName ?? typeof(TResponse).Name);
        }

        return await next().ConfigureAwait(false);
    }
}
