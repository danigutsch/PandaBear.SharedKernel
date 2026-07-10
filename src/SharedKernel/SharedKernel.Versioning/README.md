# SharedKernel.Versioning

Reusable SemVer and Conventional Commit release-impact primitives.

The package keeps version policy independent from CI, release workflows, and application hosts.

It also owns reusable release-artifact, local package-feed, and public API baseline helpers used by
repo-owned tools. Tool projects should keep command parsing, process execution, and standard IO wiring
outside this package.
