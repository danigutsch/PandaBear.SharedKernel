# AGENTS.md

Instructions for `src/SharedKernel/SharedKernel.AspNetCore/`.

## Scope and precedence

- Applies only to files under `src/SharedKernel/SharedKernel.AspNetCore/`.
- Intended for reusable ASP.NET Core primitives that applications explicitly opt into.
- The ASP.NET Core family is split by concern. This core stays light (shared framework and `SharedKernel.InputNormalization` only)
  and holds security headers, forwarded headers, CORS, the unhandled-exception handler and the telemetry names. Add a new
  package for a concern that needs another dependency instead of widening the core:
  `SharedKernel.AspNetCore.Authentication` (the only JwtBearer consumer), `SharedKernel.AspNetCore.RateLimiting`,
  `SharedKernel.AspNetCore.Seo` and `SharedKernel.Idempotency.AspNetCore`.
- The core exposes its internal telemetry helpers and `SecurityEventReporter` to the Authentication and RateLimiting packages
  with `InternalsVisibleTo`; keep them internal rather than publishing them.

## Rules

- Keep this project focused on host-agnostic ASP.NET Core helpers.
- Do not add application-specific policy names, limits, origins, routes, roles, or claims. Provider-neutral
  claim-type conventions used by configurable host mappings are allowed.
- Public APIs should include XML doc comments and remain safe for reuse by multiple ASP.NET Core projects.
- Prefer small extension methods over framework layers.

## References

- `AGENTS.md` (repository root)
- `docs/CODING_GUIDELINES.md`
