using Microsoft.CodeAnalysis;

namespace PandaBear.Messaging.IntegrationEvents.SourceGenerator;

internal sealed record IntegrationEventRegistrationCandidate(
    IntegrationEventRegistrationModel? Registration,
    string IntegrationEventType,
    Location Location);
