# SharedKernel.Aspire.Hosting.Tests

Tests for the container resources in `src/SharedKernel/SharedKernel.Aspire.Hosting.ClamAv` and
`src/SharedKernel/SharedKernel.Aspire.Hosting.SeaweedFs`.

The tests build an Aspire application model in process with
`DistributedApplication.CreateBuilder` and read the annotations of the resources. They never start
the application, so they need no container runtime and run in the `library` slice.

## Scope

- Volume lifetime of `AddClamAv` and `AddSeaweedFs`:
    - Without the `DcpPublisher:ResourceNameSuffix` configuration value, the resource mounts the named
      volume `{name}-definitions` or `{name}-data`. A restarted AppHost reuses it.
    - With a suffix, the resource is an isolated instance, such as a parallel test run. It mounts an
      anonymous volume, which the container runtime removes together with the container.
    - A blank suffix does not isolate the instance.

## What the tests do not show

The tests pin the shape of the mount. That the container runtime removes an anonymous volume with its
container is behavior of the Aspire orchestrator and the runtime, not of this library, so it is not
asserted here. To check it for a runtime by hand, start the resource with
`--DcpPublisher:ResourceNameSuffix=<unique value>`, list the volumes with `podman volume ls` or
`docker volume ls` before the application starts and after it stops, and expect no new volume.

## See also

- [tests/README.md](../README.md)
- [SharedKernel.Aspire.Hosting.ClamAv](../../src/SharedKernel/SharedKernel.Aspire.Hosting.ClamAv/README.md)
- [SharedKernel.Aspire.Hosting.SeaweedFs](../../src/SharedKernel/SharedKernel.Aspire.Hosting.SeaweedFs/README.md)
