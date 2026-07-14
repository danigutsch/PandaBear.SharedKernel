# AGENTS.md

Instructions for `src/SharedKernel/SharedKernel.AspNetCore/`.

## Scope and precedence

- Applies only to files under `src/SharedKernel/SharedKernel.AspNetCore/`.
- Intended for reusable ASP.NET Core primitives that applications explicitly opt into.

## Rules

- Keep this project focused on host-agnostic ASP.NET Core helpers.
- Do not add application-specific policy names, limits, origins, routes, roles, or claims. Provider-neutral
  claim-type conventions used by configurable host mappings are allowed.
- Public APIs should include XML doc comments and remain safe for reuse by multiple ASP.NET Core projects.
- Prefer small extension methods over framework layers.

## References

- `AGENTS.md` (repository root)
- `docs/CODING_GUIDELINES.md`
