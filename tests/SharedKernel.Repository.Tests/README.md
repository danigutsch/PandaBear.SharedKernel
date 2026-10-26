# SharedKernel.Repository.Tests

Tests for the repository tooling and conventions: the CI tool (`tools/SharedKernel.CI.Tool`), candidate staging, and the
boundary, packaging, and CI configuration conventions of the repository.

## Scope

- `Conventions/` pins what repository files must contain, such as pinned container images, packable projects, and the slices
  of `.config/sharedkernel-ci.json`.
- The other tests run the CI tool against fake executables, release servers, and service hosts.

## See Also

- [tests/README.md](../README.md)
