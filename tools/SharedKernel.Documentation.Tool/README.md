# SharedKernel.Documentation.Tool

Local .NET tool for generated documentation checks and refreshes.

## Usage

```text
dotnet run --project tools/SharedKernel.Documentation.Tool -- generate --config docs/architecture/generated-diagrams.json
dotnet run --project tools/SharedKernel.Documentation.Tool -- generate --config docs/architecture/generated-diagrams.json --check
dotnet run --project tools/SharedKernel.Documentation.Tool -- check --config docs/architecture/documentation-conformance.json
```

Repository scripts may wrap the command. In this repository, use:

```text
bash scripts/update-architecture-diagrams.sh
bash scripts/update-architecture-diagrams.sh --check
```

## Command behavior

- `generate` refreshes all configured generated Markdown blocks.
- `--check` detects drift and exits non-zero without writing files.
- `--config` points to the repository-owned generator config file.
- Every block declares one target Markdown file relative to the configured `docsPath`.
- Missing, duplicate, reversed, split, or misplaced generated markers fail before any file is written.
- Rooted targets, symbolic-link or junction paths, and generated marker text are rejected.
- `check` validates machine-readable documentation facts against the configured source files without
  writing documents.

The tool is a thin CLI over `src/SharedKernel.Documentation`; it contains no parsing or
ViajantesTurismo-specific rules. Repository-specific system maps, labels, source paths, and
conformance checks belong in config.
