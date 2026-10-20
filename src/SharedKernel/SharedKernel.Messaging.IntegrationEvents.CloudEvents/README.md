# SharedKernel.Messaging.IntegrationEvents.CloudEvents

CloudEvents metadata adapter for typed integration events.

This project keeps transport metadata mapping separate from the core integration event contracts.

## Usage

Call `CloudEventMapper.ToCloudEvent` with a typed integration event and `CloudEventMetadata` to obtain `CloudEventEnvelope<TIntegrationEvent>`. Configure source, subject, content type, and schema explicitly. Keep transport mapping outside domain event contracts.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents.CloudEvents`. C# namespace: `PandaBear.Messaging.IntegrationEvents.CloudEvents`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents.CloudEvents`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
