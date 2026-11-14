# SharedKernel.ServiceDefaults

Aspire service defaults for ASP.NET Core and worker hosts, as one package instead of a `*.ServiceDefaults` project copied into every
repository. It follows the Aspire service-defaults contract: one call adds OpenTelemetry, health checks, service discovery and HTTP
resilience, and one call maps `/health` and `/alive`. Telemetry goes through the
[SharedKernel.Observability](../SharedKernel.Observability/README.md) privacy baseline.

```csharp
using PandaBear.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharedKernelServiceDefaults(options => options.SensitiveTelemetryAttributes.Add("customer.document"));

var app = builder.Build();
app.MapSharedKernelDefaultEndpoints();
app.Run();
```

## What `AddSharedKernelServiceDefaults` adds

| Concern | What |
| --- | --- |
| Service discovery | `AddServiceDiscovery()`, and service discovery on every `HttpClient` |
| HTTP resilience | `AddStandardResilienceHandler()` on every `HttpClient`, outside the discovery handler so each retry resolves again; unsafe methods are never retried; off with `AddResilience = false` |
| OpenTelemetry | `ConfigureOpenTelemetry()` (runtime metrics, service identity, log and span privacy processors), ASP.NET Core and `HttpClient` instrumentation, the application's own activity source, and every SharedKernel meter and source (`AddSharedKernelInstrumentation`) |
| Privacy | The shared baseline, plus the attribute names in `SensitiveTelemetryAttributes`; exceptions are not recorded on request spans |
| Export | The OTLP exporter, only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (Aspire sets it for every resource) |
| Health | A liveness check named `self`, tagged `ServiceDefaultsExtensions.LiveTag` (`live`) |
| Tracing filter | Requests to the health and liveness paths are not traced |

The privacy processors are registered before the OTLP exporter, so they run first. A host that adds an exporter of its own adds it after
this call for the same reason.

## Health endpoints

`MapSharedKernelDefaultEndpoints` maps `HealthPath` (`/health`, every check: readiness) and `AlivePath` (`/alive`, only checks tagged
`live`: liveness). Both allow anonymous requests, set `Cache-Control: no-store`, and answer only the overall status as `text/plain`
(`Healthy`, `Degraded` or `Unhealthy`). They never show a check name, an exception or a dependency detail.

`Exposure` decides when they are mapped. The default is `HealthEndpointExposure.Always`, unlike the Aspire template, which maps them only in
Development: an AppHost's `WithHttpHealthCheck("/health")` and `WaitFor` need the endpoint in whatever environment the project runs, and a
container orchestrator probes a production host. Use `HealthEndpointExposure.DevelopmentOnly` for a host that must not answer probes in
production, and then add the AppHost health check only in Development too (see `WithHttpHealthCheckInAppHostEnvironment` in
[SharedKernel.Aspire.Hosting](../SharedKernel.Aspire.Hosting/README.md)).

Calling `MapSharedKernelDefaultEndpoints` without `AddSharedKernelServiceDefaults` throws `InvalidOperationException`. Paths that do not
start with `/`, or the same path for both endpoints, throw `ArgumentException`.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `HealthPath` | `/health` | Readiness endpoint, every check |
| `AlivePath` | `/alive` | Liveness endpoint, checks tagged `live` |
| `Exposure` | `Always` | `Always` or `DevelopmentOnly` |
| `AddResilience` | `true` | Standard resilience handler on every `HttpClient` |
| `ConfigureResilience` | `null` | Changes the shared standard resilience options; unsafe-method retries stay off |
| `SensitiveTelemetryAttributes` | empty | Extra attribute names to remove from logs and spans, compared without case; they cannot permit a name the baseline removes |

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ServiceDefaults`. C# namespace: `PandaBear.ServiceDefaults`. Assembly/project identity: `SharedKernel.ServiceDefaults`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
