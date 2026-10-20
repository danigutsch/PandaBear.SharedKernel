# Contributing

Read [development guidance](docs/development.md), [architecture decisions](docs/README.md), and [release policy](docs/release-policy.md). Work in repository-local `.worktrees/` worktrees and preserve other contributors' work. Keep application adoption in its owning repository.

Use Conventional Commits with a header of at most 100 characters and body lines wrapped at 100 characters. Mark breaking API changes explicitly. Sign commits using your existing signing setup; do not change global Git configuration. Run the documented build, tests, formatting, diagram, and immutable package checks before review. Candidate evidence must identify a clean committed revision.

Do not commit credentials, local feeds, raw browser traces, private captures, or generated build artifacts. New dependencies require review of licensing and transitive supply-chain risk.
