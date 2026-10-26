# SharedKernel.Aspire.Hosting.Tests

Tests for the ClamAV, SeaweedFS, and Grafana LGTM resources in `src/SharedKernel/SharedKernel.Aspire.Hosting.*`.

The tests build an Aspire application model in process and read the annotations of the resources. They never start the
application, so they need no container runtime.

## See also

- [tests/README.md](../README.md)
