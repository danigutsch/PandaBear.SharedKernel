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

Reference this package from any project needing OpenTelemetry basics. Add `.ConfigureOpenTelemetry()`
extension in your application startup and supply any required parameters for service identification.

## Current Contents

- `ExplicitServiceNameDetector` — Ensures OpenTelemetry service.name is set via code config
- `ObservabilityBuilderExtensions` — Extension(s) for standardized, DRY OpenTelemetry builder setup
- `PrivacyDataClassifications` — Technical personal, sensitive, credential, and financial classifications for source-generated logging parameters

`ConfigureOpenTelemetry()` enables classified logging redaction with the default erasing fallback.
Applications remain responsible for export-bound trace and exception sanitization because those
controls depend on the telemetry fields emitted by each application.

## Dependencies

- [OpenTelemetry](https://www.nuget.org/packages/OpenTelemetry)
- [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
- [OpenTelemetry.Instrumentation.Runtime](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
- [Microsoft.Extensions.Compliance.Redaction](https://www.nuget.org/packages/Microsoft.Extensions.Compliance.Redaction)

## Telemetry privacy processors

`ActivityPrivacyProcessor` and `LogRecordPrivacyProcessor` remove sensitive telemetry before export.
Both accept an optional `Func<string, bool>` for additional application-sensitive attribute names.
The shared baseline always applies; the callback cannot permit attributes rejected by the baseline.
Application identifiers and business-specific redaction policy stay with the host.

Register both processors before exporters. This example opts into OpenTelemetry-only logging and
adds one host-owned attribute policy; the generic baseline remains mandatory:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using PandaBear.Observability;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.ConfigureOpenTelemetry();

static bool IsPrivateAttribute(string name) =>
    string.Equals(name, "private.identifier", StringComparison.OrdinalIgnoreCase);

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = false;
    logging.IncludeScopes = false;
    logging.AddProcessor(_ => new LogRecordPrivacyProcessor(IsPrivateAttribute));
});
builder.Services.AddOpenTelemetry().WithTracing(tracing =>
    tracing.AddProcessor(new ActivityPrivacyProcessor(IsPrivateAttribute)));

// Add the host's exporters after the privacy processors.
```

`ConfigureOpenTelemetry()` enables classified logging redaction but does not register these processors.
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

Package ID: `PandaBear.SharedKernel.Observability`. C# namespace: `PandaBear.Observability`. Assembly/project identity: `SharedKernel.Observability`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
