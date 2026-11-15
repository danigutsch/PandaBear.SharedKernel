# SharedKernel.Aspire.Hosting.Tests

Tests for `SharedKernel.Aspire.Hosting` and its `.ClamAv`, `.SeaweedFs`, `.Sqlite`, `.Grafana` and `.Umami` integrations.

## Scope

The tests build an Aspire application model in process and read the annotations of the resources. They never start the
application, so they need no container runtime.

## See Also

- [tests/README.md](../README.md)
