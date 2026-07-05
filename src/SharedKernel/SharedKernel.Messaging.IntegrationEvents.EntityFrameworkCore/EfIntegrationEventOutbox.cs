using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.CloudEvents;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Stores integration events in the current EF Core unit of work.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutbox<TContext> : IIntegrationEventOutbox
    where TContext : DbContext
{
    private const string JsonContentType = "application/json";
    private const string Source = "urn:sharedkernel:integration-events";
    private const string TraceParentExtensionAttribute = "traceparent";
    private const string TraceStateExtensionAttribute = "tracestate";

    private readonly TContext? dbContext;
    private readonly TimeProvider timeProvider;
    private readonly IIntegrationEventSerializer serializer;

    public EfIntegrationEventOutbox(
        TimeProvider timeProvider,
        IIntegrationEventSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(serializer);

        this.timeProvider = timeProvider;
        this.serializer = serializer;
    }

    public EfIntegrationEventOutbox(
        TContext dbContext,
        TimeProvider timeProvider,
        IIntegrationEventSerializer serializer)
        : this(timeProvider, serializer)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        ResolveDbContext().Set<IntegrationEventOutboxMessage>().Add(new IntegrationEventOutboxMessage(
            Guid.CreateVersion7(),
            CloudEventConstants.Spec,
            CloudEventConstants.SpecVersion,
            integrationEvent.EventId.ToString("D"),
            new Uri(Source),
            TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            null,
            JsonContentType,
            null,
            serializer.Serialize(integrationEvent),
            EventPayloadEncoding.Json,
            CreateTraceExtensionAttributesJson(),
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }

    private static string? CreateTraceExtensionAttributesJson()
    {
        var currentActivity = Activity.Current;
        if (currentActivity?.Id is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentActivity.TraceStateString))
        {
            return $$"""{"{{TraceParentExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.Id)}}","{{TraceStateExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.TraceStateString)}}"}""";
        }

        return $$"""{"{{TraceParentExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.Id)}}"}""";
    }

    private TContext ResolveDbContext()
    {
        if (dbContext is not null)
        {
            return dbContext;
        }

        if (CurrentSaveChangesDbContext.Current is TContext currentDbContext)
        {
            return currentDbContext;
        }

        throw new InvalidOperationException($"No current {typeof(TContext).Name} SaveChanges context is available for the integration event outbox.");
    }
}
