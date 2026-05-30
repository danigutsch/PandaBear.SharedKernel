# AGENTS.md

Instructions for `tests/SharedKernel.OpenApi.Tests/`.

## Scope and precedence

- Applies only to files under `tests/SharedKernel.OpenApi.Tests/`.
- Use this project for generic tests of reusable OpenAPI helpers.

## Rules

- Keep tests generic and SharedKernel-focused.
- Do not add Admin-specific production behavior assertions here.
- Prefer lightweight host-backed tests through `IOpenApiDocumentProvider` over
  heavier browser or TCP-hosted approaches.

## References

- `tests/AGENTS.md`
- `src/SharedKernel/SharedKernel.OpenApi/AGENTS.md`
