using PandaBear.Messaging.Protobuf.TestContracts;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Records the <see cref="TourCreated"/> Protobuf events it handles; concrete so the consumer generator finds it.</summary>
internal sealed class CapturingTourCreatedHandler : CapturingHandler<TourCreated>;
