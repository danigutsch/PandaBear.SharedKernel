using System.Runtime.CompilerServices;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Provides nested test-only types used by the reference-dispatcher correctness tests.
/// </summary>
internal static class ReferenceDispatcherTestTypes
{
    /// <summary>
    /// Request for a tour by code whose handler answers with the upper-cased code.
    /// </summary>
    /// <param name="Code">The tour code the handler upper-cases.</param>
    internal sealed record LookupTour(string Code) : IRequest<string>;

    /// <summary>
    /// Command that deletes a tour by id and returns no value.
    /// </summary>
    /// <param name="Id">The id recorded by the delete handler.</param>
    internal sealed record DeleteTour(int Id) : ICommand;

    /// <summary>
    /// Stream request that asks for a fixed number of items.
    /// </summary>
    /// <param name="Count">How many items the stream handler yields.</param>
    internal sealed record StreamTours(int Count) : IStreamRequest<string>;

    /// <summary>
    /// Non-sealed notification that DerivedNotification inherits from, used to check handler selection for inherited notifications.
    /// </summary>
    /// <param name="Name">The name the handlers record in the event log.</param>
    internal record BaseNotification(string Name) : INotification;

    /// <summary>
    /// Notification derived from BaseNotification, so both base and derived handlers match it.
    /// </summary>
    /// <param name="Name">The name passed through to the base notification.</param>
    internal sealed record DerivedNotification(string Name) : BaseNotification(Name);

    /// <summary>
    /// Request handler that logs Handler and the code, then returns the code upper-cased.
    /// </summary>
    /// <param name="events">The shared log that records the order handlers and behaviors run in.</param>
    internal sealed class LookupTourHandler(List<string> events) : IRequestHandler<LookupTour, string>
    {
        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            events.Add($"Handler:{request.Code}");
            return ValueTask.FromResult(request.Code.ToUpperInvariant());
        }
    }

    /// <summary>
    /// Command handler that logs Delete and the id, then returns Unit.
    /// </summary>
    /// <param name="events">The shared log that records the order handlers and behaviors run in.</param>
    internal sealed class DeleteTourHandler(List<string> events) : ICommandHandler<DeleteTour>
    {
        /// <inheritdoc />
        public ValueTask<Unit> Handle(DeleteTour request, CancellationToken ct)
        {
            events.Add($"Delete:{request.Id}");
            return ValueTask.FromResult(Unit.Value);
        }
    }

    /// <summary>
    /// Observability-stage pipeline behavior that logs before and after the next step, used to check it wraps the validation stage.
    /// </summary>
    /// <param name="events">The shared log that records the order handlers and behaviors run in.</param>
    [PipelineOrder(PipelineStage.Observability)]
    internal sealed class ObservabilityPipeline(List<string> events) : IPipelineBehavior<LookupTour, string>
    {
        /// <inheritdoc />
        public async ValueTask<string> Handle(
            LookupTour request,
            RequestHandlerContinuation<string> next,
            CancellationToken ct)
        {
            events.Add("Observability:Before");
            var response = await next().ConfigureAwait(false);
            events.Add("Observability:After");
            return response;
        }
    }

    /// <summary>
    /// Validation-stage (order 10) pipeline behavior that logs before and after the next step, used to check its position relative to other stages.
    /// </summary>
    /// <param name="events">The shared log that records the order handlers and behaviors run in.</param>
    [PipelineOrder(PipelineStage.Validation, Order = 10)]
    internal sealed class ValidationPipeline(List<string> events) : IPipelineBehavior<LookupTour, string>
    {
        /// <inheritdoc />
        public async ValueTask<string> Handle(
            LookupTour request,
            RequestHandlerContinuation<string> next,
            CancellationToken ct)
        {
            events.Add("Validation:Before");
            var response = await next().ConfigureAwait(false);
            events.Add("Validation:After");
            return response;
        }
    }

    /// <summary>
    /// Handler for BaseNotification that logs the notification name with a Base prefix.
    /// </summary>
    /// <param name="events">The shared log that records which handlers ran and in what order.</param>
    internal sealed class BaseNotificationHandler(List<string> events) : INotificationHandler<BaseNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(BaseNotification notification, CancellationToken ct)
        {
            events.Add($"Base:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// First handler for DerivedNotification that logs the name with a Derived-1 prefix.
    /// </summary>
    /// <param name="events">The shared log that records which handlers ran and in what order.</param>
    internal sealed class DerivedNotificationHandlerOne(List<string> events) : INotificationHandler<DerivedNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
        {
            events.Add($"Derived-1:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Second handler for DerivedNotification that logs the name with a Derived-2 prefix.
    /// </summary>
    /// <param name="events">The shared log that records which handlers ran and in what order.</param>
    internal sealed class DerivedNotificationHandlerTwo(List<string> events) : INotificationHandler<DerivedNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
        {
            events.Add($"Derived-2:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stream handler that yields Item-1 through Item-N for the requested count, yielding to the scheduler and honoring cancellation between items.
    /// </summary>
    internal sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<string> Handle(
            StreamTours request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            for (var index = 1; index <= request.Count; index++)
            {
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                yield return $"Item-{index}";
            }
        }
    }

    /// <summary>
    /// Validation-stage stream pipeline behavior that logs before enumerating the next stream and after it completes while passing every item through.
    /// </summary>
    /// <param name="events">The shared log that records the order stream behaviors run in.</param>
    [PipelineOrder(PipelineStage.Validation, Order = 10)]
    internal sealed class StreamValidationPipeline(List<string> events) : IStreamPipelineBehavior<StreamTours, string>
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<string> Handle(
            StreamTours request,
            StreamHandlerContinuation<string> next,
            [EnumeratorCancellation] CancellationToken ct)
        {
            events.Add("StreamValidation:Before");
            await foreach (var item in next().WithCancellation(ct))
            {
                yield return item;
            }

            events.Add("StreamValidation:After");
        }
    }
}
