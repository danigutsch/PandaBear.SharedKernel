namespace PandaBear.Mediator;

/// <summary>
/// Adds optional activity-based observability around mediator request dispatch: a <c>mediator.handle</c> span tagged with the
/// request and response type names, the outcome and, on failure, the full exception type name.
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

        using var activity = MediatorActivitySource.StartRequestActivity();
        if (activity is not null)
        {
            activity.SetTag(MediatorTelemetry.TagRequestName, MediatorActivitySource.GetTypeName(typeof(TRequest)));
            activity.SetTag(MediatorTelemetry.TagResponseName, MediatorActivitySource.GetTypeName(typeof(TResponse)));
        }

        try
        {
            var response = await next().ConfigureAwait(false);
            activity?.SetTag(MediatorTelemetry.TagOutcome, MediatorTelemetry.OutcomeSuccess);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
            return response;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            activity?.SetTag(MediatorTelemetry.TagOutcome, MediatorTelemetry.OutcomeCancelled);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetTag(MediatorTelemetry.TagErrorType, ex.GetType().FullName);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            activity?.SetTag(MediatorTelemetry.TagOutcome, MediatorTelemetry.OutcomeError);
            throw;
        }
    }
}
