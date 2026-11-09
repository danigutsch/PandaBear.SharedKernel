---
name: ddd-rich-domain-model
description: Audit and refactor .NET domain code toward a rich, always-valid, persistence-ignorant model with PandaBear.SharedKernel. Use for anemic entities, public setters, exposed mutable collections, primitive obsession, exceptions for expected business outcomes, validation duplicated outside the domain, child entities changed outside their aggregate root, handlers or UI deciding from raw domain state, SKDOM001 to SKDOM003 diagnostics, unique-rule and concurrency handling. Not for glossary work or EF mapping-only changes.
license: See repository LICENSE.txt
---

# Rich domain model with SharedKernel

Use this skill to find domain rules that leak out of the domain model and move them into aggregates and value objects. Read the touched flow and the nearest `AGENTS.md` first.

## Model rules

- **Always valid**: invalid instances cannot be built. Constructors are private; callers use a static factory returning `Result<T>` (`using PandaBear.Results;`). Expected business outcomes are `Result` values, never exceptions. See `docs/adr/20251108-result-pattern-over-exceptions.md`.
- **Validate, then build** (convention, not a SharedKernel API): `Validate(raw input)` collects every field error and returns the checked data as a small immutable record. `Create(data)` and `Revise(data)` take that record, are infallible, and only assign. Keep the Validate step the only place a rule is written. Collect errors with `ValidationErrors` (`Add`, `HasErrors`, `ToResult<T>()`).
- **Value objects for domain fields**: an email, money amount, date range, or code is a type, not a `string` or `decimal`. Derive from `PandaBear.BuildingBlocks.ValueObject`, keep the constructor private, expose `Create` returning `Result<T>`, and keep a `Restore` only for persisted data that was already validated. Prefer an existing building block (`EmailAddress`, `PersonName`, `PhoneNumber`, `PostalAddress`, `CountryCode`, `Money`, `Currency`, `DateRange`, `BirthDate`, `Cpf`, `Cnpj`, `Iban`) over a local copy. For a scalar identifier or code, a `readonly partial record struct` with `[GenerateValueObject(UnderlyingType = ...)]` (`PandaBear.SharedKernel.Domain.SourceGenerator`) generates the mechanical support. No primitive obsession: a method taking two `string` arguments that mean different things needs types.
- **Aggregates own invariants**: state changes only through intent-revealing methods returning `Result`. Child entities expose `internal` mutators; only the aggregate root changes them. Implement `IAggregateRoot<TId>` (`PandaBear.Domain`) on roots that record domain events (`GetDomainEvents`, `ClearDomainEvents`).
- **Facts versus decisions**: public state is for display, mapping, and simple filtering. A decision that combines state (can publish, is full, may cancel) is a named domain method or calculated property. Handlers, endpoints, and UI call it instead of recombining raw properties.
- **Rules needing persistence or other aggregates** (uniqueness, cross-aggregate checks) stay in the application layer; do not push a repository into the domain.
- **Unique rules**: a pre-check cannot stop a concurrent insert. Name the rule on the index with `HasUniqueRule("entity.field")`, save through `SaveChangesTranslatingUniqueViolations(parser, ct)`, and map `UniqueConstraintViolationException.Rule` back to the same validation result as the pre-check (`PandaBear.SharedKernel.EntityFrameworkCore`).
- **Concurrency**: configure a concurrency token in the EF mapping (infrastructure, not the domain). `SaveChangesTranslatingConflicts(parser, ct)` turns `DbUpdateConcurrencyException` into `ConcurrencyConflictException`; catch it in the handler and return `Result.Conflict(...)`.
- **Purity**: domain projects do not reference EF Core, ASP.NET Core, JSON, logging, or HTTP. Mapping, converters, and backing fields live in infrastructure (`docs/adr/20260621-domain-materialization-and-sharedkernel-persistence-boundaries.md`).
- **No ambient clock**: take the date or `TimeProvider` as an argument (`SKDOM003`).

## Enforcement

Reference `PandaBear.SharedKernel.Domain.Analyzers` (and `.CodeFixes`) from domain projects only; see `src/SharedKernel/SharedKernel.Domain.Analyzers/README.md`.

| ID | Reports | Fix |
| --- | --- | --- |
| `SKDOM001` | Non-private, non-`init` setter | Private setter plus an intent-revealing method (code fix available) |
| `SKDOM002` | Mutable collection type visible outside the assembly | Private collection, expose `IReadOnlyCollection<T>` or `IReadOnlyList<T>` |
| `SKDOM003` | `DateTime.Now`, `UtcNow`, `Today`, `DateTimeOffset.Now`, `UtcNow` | Pass the time in or inject `TimeProvider` |

Pair them with CA1002, CA1819, CA2227, and CA1051. In the application layer, `PandaBear.SharedKernel.Results.Analyzers` reports a discarded `Result` (`SKRES002`). Never suppress these diagnostics to get a build through; if an exception is justified, record it narrowly in the review.

## Workflow

1. **Scope** one bounded context and, if possible, one aggregate.
2. **Audit**; review every hit by hand. Suggested searches (adjust paths):
   - `rg -n 'public \w[^;]* \{ get; set; \}' <domain-path>` for open setters.
   - `rg -n 'throw new' <domain-path>` for exceptions used as business outcomes.
   - `rg -n '\)\.Value\b' <application-path>` for `Result<T>.Value` read without checking success.
   - `rg -n 'if \(.*\.\w+ .*&& .*\.\w+' <handlers-and-ui-paths>` for decisions combined from raw state.
   - Fields typed `string`, `decimal`, or `Guid` that name an email, price, code, or identifier.
3. **Classify** each hit: calculated property, domain method, value object, enum, explicit transition, application-level rule, or no change (display, mapping, projection, input guard).
4. **Prove** each real gap with a failing test before changing production code.
5. **Change** the smallest thing: move the rule into the aggregate or value object, update callers, delete duplicated checks.
6. **Verify**: `dotnet build` (analyzers fail the build when warnings are errors) and `dotnet test --solution <solution>.slnx`.

## Guardrails

- No strategy objects, specifications, factories, or state hierarchies for a single simple switch.
- Do not hide state that display, mapping, or filtering legitimately needs.
- Do not add a dependency to the domain project to avoid writing a small rule; check SharedKernel building blocks first.
- Do not invent SharedKernel APIs. Confirm a type or method in the package README or source before using it; if it is missing, improve SharedKernel rather than forking the logic.
