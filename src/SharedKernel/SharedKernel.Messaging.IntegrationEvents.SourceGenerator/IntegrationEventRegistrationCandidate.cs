using Microsoft.CodeAnalysis;

namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

internal sealed record IntegrationEventRegistrationCandidate(
    IntegrationEventRegistrationModel? Registration,
    string IntegrationEventType,
    Location Location);
