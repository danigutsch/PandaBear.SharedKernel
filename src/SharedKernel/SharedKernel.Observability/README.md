# SharedKernel.Observability

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Centralized, reusable observability and OpenTelemetry configuration for all service and library projects in the solution.

## Purpose

This project provides:

- Shared OpenTelemetry setup and helpers for logging, metrics, and tracing
- Code-first service identity configuration (e.g., explicit service.name)
- Composable extension methods for consistent instrumentation and exporter setup
- No feature-coupled logic—only service-agnostic/cross-cutting observability constructs

## Usage

Reference this package from any project needing OpenTelemetry basics. Add `.AddObservability()`
extension in your application startup and supply any required parameters for service identification.

## Current Contents

- `ExplicitServiceNameDetector` — Ensures OpenTelemetry service.name is set via code config
- `ObservabilityBuilderExtensions` — Extension(s) for standardized, DRY OpenTelemetry builder setup
- `AddTelemetryPrivacy` — Log and trace privacy processors with formatted messages and scopes excluded
- `PrivacyDataClassifications` — Technical personal, sensitive, credential, and financial classifications for source-generated logging parameters
- `AddInstrumentation()` — On `MeterProviderBuilder` and `TracerProviderBuilder`: collects the meters and traces the
  activity sources of every SharedKernel package by name prefix (`SharedKernel.*`, see `NamePatterns`)

```csharp
builder.AddObservability();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddInstrumentation())
    .WithTracing(tracing => tracing.AddInstrumentation());
```

The prefix replaces one hard-coded name per package (`SharedKernel.AspNetCore`, `SharedKernel.Blazor`, `SharedKernel.Mediator`,
`SharedKernel.EventSourcing.Npgsql`, ...) and needs no reference to those packages: a package that is not loaded creates no meter or
source, so nothing extra is exported. Every meter and activity source is named after the assembly of the package that creates it, as
the [telemetry standard](../../../docs/telemetry.md) requires and a repository convention test enforces. Each
package still exposes its own name constant (for example `AspNetCoreTelemetry.MeterName`) for hosts that register names one by one.
The call is opt-in rather than part of `AddObservability()`, so existing hosts export nothing new until they ask for it.

`AddObservability()` enables classified logging redaction with the default erasing fallback.
Applications remain responsible for export-bound trace and exception sanitization because those
controls depend on the telemetry fields emitted by each application.

## Dependencies

- [OpenTelemetry](https://www.nuget.org/packages/OpenTelemetry)
- [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
- [OpenTelemetry.Instrumentation.Runtime](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
- [Microsoft.Extensions.Compliance.Redaction](https://www.nuget.org/packages/Microsoft.Extensions.Compliance.Redaction)

## Telemetry privacy processors

`ActivityPrivacyProcessor` and `LogRecordPrivacyProcessor` remove sensitive telemetry before export.
`ActivityPrivacyProcessor` also removes PostgreSQL connection and object attributes from client
spans and gives them bounded display names. Exception-event handling belongs to the export pipeline.
Both accept an optional `Func<string, bool>` for additional application-sensitive attribute names.
The shared baseline always applies; the callback cannot permit attributes rejected by the baseline.
Application identifiers and business-specific redaction policy stay with the host.

`AddObservability()` already excludes formatted messages and scopes from OpenTelemetry logs and
registers both processors, so the generic baseline applies by default. `AddTelemetryPrivacy()` adds one
host-owned attribute policy on top; on its own it applies the baseline too, and the baseline is registered
once however the two are combined. Call both before adding exporters. This example opts into
OpenTelemetry-only logging and adds one host-owned attribute policy:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Observability;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.AddObservability();

static bool IsPrivateAttribute(string name) =>
    string.Equals(name, "private.identifier", StringComparison.OrdinalIgnoreCase);

builder.AddTelemetryPrivacy(IsPrivateAttribute);

// Add the host's exporters after the privacy processors.
```

`AddObservability()` also enables classified logging redaction with the default erasing fallback.
The host selects additional private attributes, logging providers, instrumentation, and exporters.
The shared baseline removes sensitive HTTP, database, AWS/S3, object-storage, and messaging attributes
while retaining operational fields such as route templates, methods, status codes, and operation names.
URL redaction applies to all activities, including activities without AWS or bucket attributes.

The log processor retains the original message template, clears formatted messages and exception
objects, and preserves only a bounded exception type. Use fixed templates and classified parameters;
secrets embedded directly in a template cannot be removed by attribute-name filtering. Trace processors
remove exception-valued tags and status descriptions; they do not rewrite activity events. Hosts using
exception-event instrumentation, including ASP.NET Core exception recording, must disable or sanitize
those events separately. Disable scope collection when scopes may contain private data.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Observability`. C# namespace: `SharedKernel.Observability`. Assembly/project identity: `SharedKernel.Observability`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
