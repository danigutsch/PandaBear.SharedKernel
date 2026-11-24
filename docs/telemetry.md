# Telemetry standard

Every SharedKernel package that emits OpenTelemetry signals follows the rules below, so a host exports them with one call and
finds them under predictable names. Packages remain preview (prerelease); a name change is a breaking change and is marked as
one in the Conventional Commit history.

## Rules

1. **Meter and ActivitySource names.** A meter or activity source is named after the assembly of the package that creates it,
   `SharedKernel.<Package>`. A package uses one meter and one source, both with the same name.
   `InstrumentationExtensions.NamePatterns` in `SharedKernel.Observability` registers `SharedKernel.*` only, so
   `metrics.AddInstrumentation()` and `tracing.AddInstrumentation()` cover every package; packages do not register their own
   names.
2. **Instrument names** are `sharedkernel.<area>.<thing>`: lowercase, dotted, words inside a segment separated by `_`, no
   `_total` suffix, plural names for counts. Every instrument has a description.
3. **Durations** are `Histogram<double>` instruments with unit `s` that record `elapsed.TotalSeconds`, never milliseconds. Every
   duration histogram passes `InstrumentAdvice<double>` with the shared bucket boundaries
   `[0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]`. The boundaries are defined once, in the
   internal `DurationBuckets` class in `src/SharedKernel/SharedKernel.Mediator/DurationBuckets.cs`, and every other package that
   records a duration compiles the same file through a linked `<Compile Include="..." Link="..." />` item, as `TelemetryGuard`
   is shared. No package takes a new dependency for it.
4. **Attributes** use the OpenTelemetry semantic-convention name wherever one exists: `error.type`, `http.route`,
   `http.request.method`, `http.response.status_code`, `db.system.name` (and the resource attributes `service.name`,
   `service.version`, and `exception.type`). Any other attribute is `sharedkernel.<area>.<name>`. Outcome and other enum-like
   values are lowercase snake_case, such as `timed_out`. `error.type` for an exception is the exception's full type name
   (`exception.GetType().FullName`); a package may use a documented domain error code instead, as ClamAV does.
5. **Span names** are `<area>.<operation>`, low cardinality, without a `sharedkernel.` prefix. Variable data goes into
   attributes, never into the span name.

`TelemetryNameConventionTests` in `tests/SharedKernel.Repository.Tests` enforces rules 1 to 4 on the library sources: meter and
source names equal the creating package's assembly name, instrument names start with `sharedkernel.` and carry a description,
duration histograms use unit `s`, pass the shared advice and never record milliseconds, and attribute keys start with
`sharedkernel.` unless they are on the semantic-convention allow-list above.

Out of scope: log categories and `EventId` values, which keep their names, and the overlap with the metrics ASP.NET Core emits
itself.

## Packages

Units in braces, such as `{request}`, are OpenTelemetry annotation units for counts.

### SharedKernel.AspNetCore

Meter `SharedKernel.AspNetCore` (`AspNetCoreTelemetry.MeterName`). No activity source.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.aspnetcore.unhandled_exceptions` | Counter | `{exception}` | Unhandled exceptions by exception type and route template. | `error.type` (full type name, `AggregateException` and `TargetInvocationException` unwrapped), `http.route` (matched template or `unmatched`), `sharedkernel.aspnetcore.handled` (bool) |
| `sharedkernel.aspnetcore.authentication.events` | Counter | `{event}` | JWT bearer authentication failures, challenges and forbidden results. | `sharedkernel.aspnetcore.authentication.event`, `sharedkernel.aspnetcore.authentication.reason`, `sharedkernel.aspnetcore.authentication.scheme` |
| `sharedkernel.aspnetcore.rate_limit.rejections` | Counter | `{request}` | Requests rejected by a rate-limit policy. | `sharedkernel.aspnetcore.rate_limit.policy` |

The authentication and rate-limiting counters are registered by `SharedKernel.AspNetCore.Authentication` and
`SharedKernel.AspNetCore.RateLimiting` but created by the core package, so they share its meter. See
[ASP.NET Core error and security telemetry](aspnetcore-error-and-security-telemetry.md).

### SharedKernel.AspNetCore.WebAnalytics

Meter `SharedKernel.AspNetCore.WebAnalytics` (`WebAnalyticsTelemetry.MeterName`). No activity source.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.web_analytics.page_views.dropped` | Counter | `{page_view}` | Page views that were not delivered to a target, by target and reason. | `sharedkernel.web_analytics.target`, `sharedkernel.web_analytics.reason` (`queue_full`, `rejected`, `send_failed`, `shutdown`) |

### SharedKernel.Blazor

Meter `SharedKernel.Blazor` (`UiFailureReporter.MeterName`). No activity source.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.blazor.handled_failures` | Counter | `{failure}` | Handled UI failures by exception type and operation. | `error.type` (full type name, wrappers unwrapped), `sharedkernel.blazor.operation` |

### SharedKernel.EventSourcing.Npgsql

Meter and activity source `SharedKernel.EventSourcing.Npgsql` (`PostgreSqlEventSourcingTelemetry.Name`). Every span and
measurement carries `db.system.name` = `postgresql` and `sharedkernel.event_sourcing.schema`.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.event_sourcing.append.duration` | Histogram | `s` | Duration of PostgreSQL event stream append operations. | `sharedkernel.event_sourcing.outcome` |
| `sharedkernel.event_sourcing.load.duration` | Histogram | `s` | Duration of PostgreSQL event stream load operations. | `sharedkernel.event_sourcing.operation`, `sharedkernel.event_sourcing.outcome` |
| `sharedkernel.event_sourcing.checkpoint.duration` | Histogram | `s` | Duration of PostgreSQL projection checkpoint operations. | `sharedkernel.event_sourcing.operation`, `sharedkernel.event_sourcing.outcome` |
| `sharedkernel.event_sourcing.events.appended` | Counter | `{event}` | Number of events appended to PostgreSQL event streams. | none beyond the common ones |
| `sharedkernel.event_sourcing.events.loaded` | Counter | `{event}` | Number of events loaded from PostgreSQL event streams. | `sharedkernel.event_sourcing.operation` |
| `sharedkernel.event_sourcing.append.conflicts` | Counter | `{conflict}` | Number of PostgreSQL event stream append conflicts. | none beyond the common ones |

| Span | Attributes |
| --- | --- |
| `event_sourcing.append` | `sharedkernel.event_sourcing.operation` (`append`), `sharedkernel.event_sourcing.event_count`, `sharedkernel.event_sourcing.expected_revision.mode` (`any`, `no_stream`, `exact`), `sharedkernel.event_sourcing.expected_revision.value`, `sharedkernel.event_sourcing.actual_revision` (on a conflict), `sharedkernel.event_sourcing.outcome` |
| `event_sourcing.load` | `sharedkernel.event_sourcing.operation` (`load`, `load_after`), `sharedkernel.event_sourcing.event_count`, `sharedkernel.event_sourcing.outcome` |
| `event_sourcing.checkpoint` | `sharedkernel.event_sourcing.operation` (`get_checkpoint`, `save_checkpoint`), `sharedkernel.event_sourcing.projection.name`, `sharedkernel.event_sourcing.checkpoint.position`, `sharedkernel.event_sourcing.outcome` |

Outcomes: `success`, `conflict`, `error`.

### SharedKernel.HttpClients

Activity source `SharedKernel.HttpClients` (`HttpOutcomeMapper.ActivitySourceName`). No meter.

| Span | Attributes |
| --- | --- |
| `http_client.outcome` | `http.request.method`, `http.response.status_code` (when a response arrived), `sharedkernel.http_client.outcome`: the `HttpOutcomeKind` in snake_case (`succeeded`, `validation_problem`, `not_found`, `unauthorized`, `forbidden`, `conflict`, `empty_body`, `malformed_body`, `unexpected_status`, `unsupported_content_type`, `timed_out`, `transport_failure`) |

### SharedKernel.MalwareScanning.ClamAv

Meter and activity source `SharedKernel.MalwareScanning.ClamAv`. Every measurement and the span carry
`sharedkernel.malware_scan.engine` (`clamav`), `sharedkernel.malware_scan.transport` (`tcp`), `sharedkernel.malware_scan.mode`
(`instream`), `sharedkernel.malware_scan.outcome` (`clean`, `infected`, `error`) and, on a failure, `error.type` with a domain
error code: `timeout`, `scanner_unavailable`, `scanner_protocol_error` or `content_length_mismatch`.

| Instrument | Kind | Unit | Description |
| --- | --- | --- | --- |
| `sharedkernel.malware_scan.scans` | Counter | `{scan}` | Completed malware scans by engine and outcome. |
| `sharedkernel.malware_scan.duration` | Histogram | `s` | Duration of one malware scan. |

| Span | Kind |
| --- | --- |
| `malware_scan.scan` | Client |

### SharedKernel.Mediator

Meter and activity source `SharedKernel.Mediator` (`MediatorTelemetry.Name`, defined in `SharedKernel.Mediator.Abstractions` so
generated code in consumer assemblies can name it; the meter and source are created in `SharedKernel.Mediator`). Names below are
the members of `MediatorTelemetry`.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.mediator.requests` | Counter | `{request}` | Total mediator requests dispatched. | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.outcome` |
| `sharedkernel.mediator.request.duration` | Histogram | `s` | Duration of mediator request dispatch. | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.outcome` |
| `sharedkernel.mediator.notifications` | Counter | `{notification}` | Total mediator notifications dispatched. | `sharedkernel.mediator.notification.name`, `sharedkernel.mediator.outcome` |
| `sharedkernel.mediator.notification.duration` | Histogram | `s` | Duration of mediator notification fan-out. | `sharedkernel.mediator.notification.name`, `sharedkernel.mediator.outcome` |
| `sharedkernel.mediator.streams` | Counter | `{request}` | Total mediator stream requests dispatched. | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.outcome` |

| Span | Emitted by | Attributes |
| --- | --- | --- |
| `mediator.send` | generated `AppMediator` | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.request.assembly`, `sharedkernel.mediator.handler.name`, `sharedkernel.mediator.pipeline.depth`, `sharedkernel.mediator.outcome`, `error.type` |
| `mediator.stream` | generated `AppMediator` | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.request.assembly`, `sharedkernel.mediator.handler.name`, `sharedkernel.mediator.outcome`, `error.type` |
| `mediator.publish` | generated dispatch | `sharedkernel.mediator.notification.name`, `sharedkernel.mediator.notification.assembly`, `sharedkernel.mediator.notification.handler_count`, `sharedkernel.mediator.outcome`, `error.type` |
| `mediator.notification.handle` | generated dispatch | `sharedkernel.mediator.handler.name`, `sharedkernel.mediator.outcome`, `error.type` |
| `mediator.handle` | `ActivityBehavior<TRequest, TResponse>` | `sharedkernel.mediator.request.name`, `sharedkernel.mediator.response.name`, `sharedkernel.mediator.outcome`, `error.type` |

Request, response, notification and handler names are simple type names without namespace or generic arity, the same form in
generated spans and in `ActivityBehavior`. `sharedkernel.mediator.outcome` has one value set everywhere: `success`, `cancelled`
(the caller's token was cancelled) and `error` (any other exception).

### SharedKernel.ObjectStorage.S3

Activity source `SharedKernel.ObjectStorage.S3` (`S3ObjectStoreTelemetry.Name`). Metrics come from the AWS SDK through
`AddAWSInstrumentation()`, which the registration keeps.

| Span | Kind | Attributes |
| --- | --- | --- |
| `object_storage.{operation}`: `put`, `open_read`, `exists`, `list`, `create_upload_url`, `delete`, `health` | Client | `sharedkernel.object_storage.operation` |

### SharedKernel.Observability.Npgsql

Meter `SharedKernel.Observability.Npgsql` (`PostgreSqlIndexHealthTelemetry.MeterName`). No activity source. Every measurement
carries `db.system.name` = `postgresql`.

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.index_health.assessments` | Counter | `{assessment}` | Aggregate, advisory PostgreSQL index-health assessments. | `sharedkernel.index_health.action`, `sharedkernel.index_health.reason` |
| `sharedkernel.index_health.collections` | Counter | `{collection}` | PostgreSQL index-health collection outcomes. | `sharedkernel.index_health.outcome` (`collected`, `permission_denied`, `unsupported`, `unavailable`) |

### SharedKernel.Scheduling

Meter and activity source `SharedKernel.Scheduling` (`SchedulingTelemetry.Name`).

| Instrument | Kind | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `sharedkernel.scheduling.polling.cycle.duration` | Histogram | `s` | Duration of one polling drain cycle. | `sharedkernel.scheduling.service.name`, `sharedkernel.scheduling.outcome` |
| `sharedkernel.scheduling.polling.batches` | Counter | `{batch}` | Completed polling batches. | `sharedkernel.scheduling.service.name` |
| `sharedkernel.scheduling.polling.items` | Counter | `{item}` | Processed polling work items. | `sharedkernel.scheduling.service.name` |
| `sharedkernel.scheduling.polling.failures` | Counter | `{failure}` | Non-fatal polling failures. | `sharedkernel.scheduling.service.name`, `error.type` |

| Span | Attributes |
| --- | --- |
| `scheduling.poll` | `sharedkernel.scheduling.service.name`, `sharedkernel.scheduling.outcome` (`success`, `cancelled`, `error`), `sharedkernel.scheduling.item_count`, `error.type` |
