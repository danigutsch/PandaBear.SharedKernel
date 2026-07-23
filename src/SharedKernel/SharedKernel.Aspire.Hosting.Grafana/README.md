# SharedKernel.Aspire.Hosting.Grafana

Reusable .NET Aspire hosting extensions for local Grafana LGTM observability stacks.

The package adds typed resources and extension methods for Grafana, Loki, Tempo,
Prometheus, and the OpenTelemetry Collector. The collector gateway composes a shared privacy
configuration with an environment-specific routing configuration before forwarding AppHost telemetry.
