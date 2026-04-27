using System.Runtime.CompilerServices;

namespace SharedKernel.Mediator.Tests;

/// <summary>
/// Provides nested test-only types used by the reference-dispatcher correctness tests.
/// </summary>
internal static class ReferenceDispatcherTestTypes
{
    internal sealed record LookupTour(string Code) : IRequest<string>;

    internal sealed record DeleteTour(int Id) : ICommand;

    internal sealed record StreamTours(int Count) : IStreamRequest<string>;

    internal record BaseNotification(string Name) : INotification;

    internal sealed record DerivedNotification(string Name) : BaseNotification(Name);

    internal sealed class LookupTourHandler(List<string> events) : IRequestHandler<LookupTour, string>
    {
        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            events.Add($"Handler:{request.Code}");
            return ValueTask.FromResult(request.Code.ToUpperInvariant());
        }
    }

    internal sealed class DeleteTourHandler(List<string> events) : ICommandHandler<DeleteTour>
    {
        /// <inheritdoc />
        public ValueTask<Unit> Handle(DeleteTour request, CancellationToken ct)
        {
            events.Add($"Delete:{request.Id}");
            return ValueTask.FromResult(Unit.Value);
        }
    }

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

    internal sealed class BaseNotificationHandler(List<string> events) : INotificationHandler<BaseNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(BaseNotification notification, CancellationToken ct)
        {
            events.Add($"Base:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class DerivedNotificationHandlerOne(List<string> events) : INotificationHandler<DerivedNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
        {
            events.Add($"Derived-1:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class DerivedNotificationHandlerTwo(List<string> events) : INotificationHandler<DerivedNotification>
    {
        /// <inheritdoc />
        public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
        {
            events.Add($"Derived-2:{notification.Name}");
            return ValueTask.CompletedTask;
        }
    }

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
}
