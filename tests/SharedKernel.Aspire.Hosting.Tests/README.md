# SharedKernel.Aspire.Hosting.Tests

Tests for `SharedKernel.Aspire.Hosting` and its `.ClamAv`, `.SeaweedFs`, `.Sqlite`, `.Grafana` and `.Umami` integrations.

## Scope

The tests build an Aspire application model in process and read the annotations of the resources. They never start the
application, so they need no container runtime. Umami website set-up is tested against a fake Umami here; the end-to-end test with
real containers is in [SharedKernel.Aspire.Hosting.Umami.Tests](../SharedKernel.Aspire.Hosting.Umami.Tests/README.md).

## See Also

- [tests/README.md](../README.md)
