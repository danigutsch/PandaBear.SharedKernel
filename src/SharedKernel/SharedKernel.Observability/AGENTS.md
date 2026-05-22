# AGENTS.md

Instructions for SharedKernel.Observability

## Scope and precedence

- Applies only to files under `src/SharedKernel/SharedKernel.Observability/`.
- Intended for repository-wide, cross-cutting observability code/config only.
- Do not add feature-specific (e.g., Mediator) observability to this project.

## Rules

- All code should be safe for use in any service or library context (no application coupling).
- Public APIs should include XML doc comments and follow the codebase's quality standards.
- Exporter wiring, advanced detectors, or app/service-specific logic should live in consuming layers, not here.
- Update README with any major changes to core helpers/config.

## References

- [docs/CODING_GUIDELINES.md](../../../docs/CODING_GUIDELINES.md)
