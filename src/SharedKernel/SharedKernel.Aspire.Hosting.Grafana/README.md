# SharedKernel.Aspire.Hosting.Grafana

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable .NET Aspire hosting extensions for local Grafana LGTM observability stacks.

The package adds typed resources and extension methods for Grafana, Loki, Tempo,
Prometheus, and the OpenTelemetry Collector. The collector gateway composes a shared privacy
configuration with an environment-specific routing configuration before forwarding AppHost telemetry.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Grafana`. C# namespace: `PandaBear.Aspire.Hosting.Grafana`. Assembly/project identity: `SharedKernel.Aspire.Hosting.Grafana`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
