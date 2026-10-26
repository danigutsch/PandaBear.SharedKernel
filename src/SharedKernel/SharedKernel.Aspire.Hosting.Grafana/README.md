# SharedKernel.Aspire.Hosting.Grafana

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration for a local Grafana LGTM observability stack.

## Usage example

```csharp
builder.AddGrafanaLgtmStack("observability");
```

The stack adds Grafana, Loki, Tempo, Prometheus, and an OpenTelemetry Collector gateway that applies a shared privacy
configuration before it forwards the telemetry of the AppHost. Each resource is also available on its own, for example
`AddGrafana`, `AddLoki`, and `AddOpenTelemetryCollectorGateway`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Grafana`. C# namespace: `PandaBear.Aspire.Hosting.Grafana`. Assembly/project identity: `SharedKernel.Aspire.Hosting.Grafana`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
