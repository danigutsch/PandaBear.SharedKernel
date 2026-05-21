# SharedKernel.Observability

Centralized, reusable observability and OpenTelemetry configuration for all service and library projects in the solution.

## Purpose

This project provides:
- Shared OpenTelemetry setup and helpers for logging, metrics, and tracing
- Code-first service identity configuration (e.g., explicit service.name)
- Composable extension methods for consistent instrumentation and exporter setup
- No feature-coupled logic—only service-agnostic/cross-cutting observability constructs

## Usage

Reference this package from any project needing OpenTelemetry basics. Add `.ConfigureOpenTelemetry()` extension in your application startup and supply any required parameters for service identification.

## Current Contents
- `ExplicitServiceNameDetector` — Ensures OpenTelemetry service.name is set via code config
- `ObservabilityBuilderExtensions` — Extension(s) for standardized, DRY OpenTelemetry builder setup

## Dependencies
- [OpenTelemetry](https://www.nuget.org/packages/OpenTelemetry)
- [OpenTelemetry.Metrics](https://www.nuget.org/packages/OpenTelemetry)
- [OpenTelemetry.Trace](https://www.nuget.org/packages/OpenTelemetry)

## See Also
- [docs/OBSERVABILITY.md](../../../docs/OBSERVABILITY.md) (if present)
