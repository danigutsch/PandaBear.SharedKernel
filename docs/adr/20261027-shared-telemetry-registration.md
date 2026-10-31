# Shared telemetry registration

Date: 2026-10-27. Status: accepted; the opt-in privacy wiring is superseded by [Telemetry privacy by default](20261031-telemetry-privacy-by-default.md).

Hosts repeat the same OpenTelemetry registration. `SharedKernel.Observability` already owns the shared part, `ConfigureOpenTelemetry()`: redaction, service identity, runtime metrics. This record decides which of the remaining host-side registration steps move into a SharedKernel package.

## Decision

- **Moves:** the privacy wiring. `AddTelemetryPrivacy()` in `SharedKernel.Observability` excludes formatted messages and scopes from OpenTelemetry logs and registers `LogRecordPrivacyProcessor` and `ActivityPrivacyProcessor`, with an optional host predicate for extra private attributes. It adds no package reference: both processors and OpenTelemetry already ship in the package.
- **Stays in the host:** exporter selection (the OTLP endpoint variable, other backends), ASP.NET Core and gRPC instrumentation, request filters such as health-path exclusion, meter and source names, and the host's private-attribute policy. The package scope in `SharedKernel.Observability/AGENTS.md` keeps exporter wiring and application policy out of the package, and ASP.NET Core instrumentation would add `OpenTelemetry.Instrumentation.AspNetCore` to a host-agnostic package.
- **Not decided here:** disabling exception recording on ASP.NET Core instrumentation. It needs the ASP.NET Core instrumentation options type and belongs with the ASP.NET Core telemetry in `SharedKernel.AspNetCore`.

## Cost

- One public method on an existing package; no new package and no new dependency, direct or transitive.
- Calling the method after an exporter was registered would place the processors behind it. The XML documentation and README state that it is called before exporters are added.
