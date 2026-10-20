# SharedKernel.Messaging.IntegrationEvents.CloudEvents

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

CloudEvents metadata adapter for typed integration events.

This project keeps transport metadata mapping separate from the core integration event contracts.

## Usage

Call `CloudEventMapper.ToCloudEvent` with a typed integration event and `CloudEventMetadata` to obtain `CloudEventEnvelope<TIntegrationEvent>`. Configure source, subject, content type, and schema explicitly. Keep transport mapping outside domain event contracts.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents.CloudEvents`. C# namespace: `PandaBear.Messaging.IntegrationEvents.CloudEvents`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents.CloudEvents`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
