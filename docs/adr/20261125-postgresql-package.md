# PostgreSQL rules package

Date: 2026-11-25. Status: accepted.

PostgreSQL rules that need no database provider live in a new package, `PandaBear.SharedKernel.PostgreSql` (project, assembly and
namespace `SharedKernel.PostgreSql`), which has no dependency. Its first type is `PostgreSqlIdentifier`, which moved there from
`SharedKernel.InputNormalization` ([#491](https://github.com/danigutsch/PandaBear.SharedKernel/issues/491)).

## Why a separate package

- `PostgreSqlIdentifier.IsUnquoted` is a PostgreSQL naming rule, not input normalization. A reader looking for it does not look in
  InputNormalization, and InputNormalization holds generic text and number normalization.
- Its callers are of two kinds. `SharedKernel.Idempotency.EntityFrameworkCore` and
  `SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore` are provider-agnostic EF Core packages without Npgsql; Idempotency checks
  the provider by name on purpose. `SharedKernel.EventSourcing.Npgsql` uses raw Npgsql without EF Core.
  `SharedKernel.DispositionHold.EntityFrameworkCore` uses both.
- No existing package fits all of them. `SharedKernel.Npgsql` and `SharedKernel.EntityFrameworkCore.Npgsql` would bring Npgsql into the
  provider-agnostic EF packages. `SharedKernel.EntityFrameworkCore` would bring EF Core Relational into EventSourcing.Npgsql.
- Linking the source into each caller as an internal type would remove the public rule and leave a type in a namespace that does not match
  its assembly in several assemblies.

## Dependencies and cost

- One more package and preview surface, with a public API baseline, a README and a unit test project
  (`SharedKernel.PostgreSql.Tests.Unit`, in process).
- No package dependency and no new package version. The package keeps no lock file (`lockExemptProjects`).
- The four callers reference the package directly. Moving the type is breaking: its namespace changes from
  `SharedKernel.InputNormalization` to `SharedKernel.PostgreSql`.

## Rules for the package

- It holds PostgreSQL rules that run without a connection or a provider: naming, quoting and limits. Code that talks to the server stays in
  `SharedKernel.Npgsql` or the EF Core packages.
- It takes no dependency. A rule that needs Npgsql or EF Core belongs in their packages.

Registry publication, releases, and consumer adoption remain separate actions.
