using SharedKernel.Messaging.Protobuf.TestContracts;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

/// <summary>Records the <see cref="TourCreated"/> Protobuf events it handles; concrete so the consumer generator finds it.</summary>
internal sealed class CapturingTourCreatedHandler : CapturingHandler<TourCreated>;
