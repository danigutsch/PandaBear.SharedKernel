# AGENTS.md

Instructions for `src/SharedKernel/SharedKernel.OpenApi/`.

## Scope and precedence

- Applies only to files under `src/SharedKernel/SharedKernel.OpenApi/`.
- Intended for repository-wide, cross-cutting OpenAPI helpers.

## Rules

- Keep this project focused on reusable OpenAPI concerns only.
- Do not move application-specific boundary definitions into this project.
- Public APIs should include XML doc comments and remain safe for reuse by multiple API projects.
- Prefer small helper types and extensions over speculative framework layers.

## References

- `AGENTS.md` (repository root)
- `docs/CODING_GUIDELINES.md`
