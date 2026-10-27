using PandaBear.Messaging.Protobuf.TestContracts;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Records the <see cref="TourCreated"/> Protobuf events it handles.</summary>
internal sealed class CapturingTourCreatedHandler : IIntegrationEventHandler<TourCreated>
{
    /// <summary>Gets the handled events in delivery order.</summary>
    public static List<TourCreated> Handled { get; } = [];

    /// <inheritdoc />
    public ValueTask Handle(TourCreated integrationEvent, CancellationToken ct)
    {
        Handled.Add(integrationEvent);
        return ValueTask.CompletedTask;
    }
}
