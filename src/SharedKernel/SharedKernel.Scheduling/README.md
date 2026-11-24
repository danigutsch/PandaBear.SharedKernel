# SharedKernel.Scheduling

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Lightweight scheduling primitives for hosted .NET services.

## What it provides

- `PollingBackgroundService`: drains available work in batches, then waits for the next poll.
- OpenTelemetry source and meter named `SharedKernel.Scheduling`.
- Stable low-cardinality telemetry tags for service name, outcome, item count, and error type.

## What it does not provide

This package is not a durable scheduler, queue, or job store. Callers remain responsible for
persistence, locking, idempotency, retries, and business-specific state.

## Telemetry

The polling loop emits:

- Activity name: `scheduling.poll`
- Duration metric: `sharedkernel.scheduling.polling.cycle.duration`, unit `s`, with the shared duration buckets
- Batch counter: `sharedkernel.scheduling.polling.batches`
- Item counter: `sharedkernel.scheduling.polling.items`
- Failure counter: `sharedkernel.scheduling.polling.failures`

Attributes: `sharedkernel.scheduling.service.name`, `sharedkernel.scheduling.outcome` (`success`, `cancelled`, `error`),
`sharedkernel.scheduling.item_count` and `error.type` (the full exception type name). See the
[telemetry standard](../../../docs/telemetry.md).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Scheduling`. C# namespace: `PandaBear.Scheduling`. Assembly/project identity: `SharedKernel.Scheduling`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
