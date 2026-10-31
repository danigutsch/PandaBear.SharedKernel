# Telemetry privacy by default

Date: 2026-10-31. Status: accepted. Supersedes the opt-in part of [Shared telemetry registration](20261027-shared-telemetry-registration.md).

`ConfigureOpenTelemetry()` used to enable formatted messages and scopes in OpenTelemetry logs and register no privacy processor. A host had to call `AddTelemetryPrivacy()` as well, and a host that wired it by hand could override the shared defaults and let its own documentation drift from the code. A live check in ViajantesTurismo found exactly that: the package documentation described the shared defaults while the host overrode them.

## Decision

- `ConfigureOpenTelemetry()` excludes formatted messages and scopes from OpenTelemetry logs and registers `LogRecordPrivacyProcessor` and `ActivityPrivacyProcessor` with the shared baseline.
- `AddTelemetryPrivacy(predicate)` stays and only adds host-owned private attribute names. Called on its own it applies the baseline too.
- The baseline is registered once however the two calls are combined or ordered. An internal policy object collects the host predicates and the processors read it when they run.
- Exporter selection, instrumentation and the host's private-attribute policy stay in the host, as before.

## Cost

- Behavior change for hosts that call only `ConfigureOpenTelemetry()`: OTLP-bound logs lose formatted messages and scopes, and sensitive attributes are removed. Other logging providers are unaffected. The package is prerelease.
- Hosts that wanted formatted messages or scopes in OpenTelemetry logs have no opt-out. None is known, and an opt-out would reopen the drift this record closes.
- No new package and no new dependency, direct or transitive.
