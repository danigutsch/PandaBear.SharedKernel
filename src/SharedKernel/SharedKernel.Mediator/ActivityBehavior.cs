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
            activity.SetTag(MediatorTelemetry.TagRequestType, request.GetType().FullName ?? typeof(TRequest).FullName ?? typeof(TRequest).Name);
            activity.SetTag(MediatorTelemetry.TagResponseType, typeof(TResponse).FullName ?? typeof(TResponse).Name);
        }

        try
        {
            var response = await next().ConfigureAwait(false);
            activity?.SetTag(MediatorTelemetry.TagRuntimeOutcome, MediatorTelemetry.OutcomeSuccess);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
            return response;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag(MediatorTelemetry.TagRuntimeOutcome, MediatorTelemetry.OutcomeCancelled);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetTag(MediatorTelemetry.TagErrorType, ex.GetType().Name);
            activity?.AddException(ex);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.GetType().Name);
            activity?.SetTag(MediatorTelemetry.TagRuntimeOutcome, MediatorTelemetry.OutcomeError);
            throw;
        }
    }
}
