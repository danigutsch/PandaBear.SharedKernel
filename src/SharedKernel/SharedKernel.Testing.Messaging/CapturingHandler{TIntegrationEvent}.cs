using System.Diagnostics.CodeAnalysis;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Testing.Messaging;

/// <summary>
/// Integration event handler that records the events it handles in a static list per event type.
/// Unsealed so a source generator that scans for concrete handler types can see a derived handler.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type handled by the handler.</typeparam>
public class CapturingHandler<TIntegrationEvent> : IIntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    private static readonly List<TIntegrationEvent> HandledEvents = [];

    /// <summary>
    /// Gets the handled events in delivery order.
    /// </summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The container creates handlers by type, so the record is shared per closed event type.")]
    public static IList<TIntegrationEvent> Handled => HandledEvents;

    /// <inheritdoc />
    public ValueTask Handle(TIntegrationEvent integrationEvent, CancellationToken ct)
    {
        HandledEvents.Add(integrationEvent);
        return ValueTask.CompletedTask;
    }
}
