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

        try
        {
            var response = await next().ConfigureAwait(false);
            activity?.SetTag("sharedkernel.mediator.outcome", "success");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
            return response;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("sharedkernel.mediator.outcome", "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.AddException(ex);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.GetType().Name);
            activity?.SetTag("sharedkernel.mediator.outcome", "error");
            throw;
        }
    }
}
