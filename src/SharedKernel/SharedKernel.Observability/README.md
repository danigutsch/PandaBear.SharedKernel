# SharedKernel.Observability

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
