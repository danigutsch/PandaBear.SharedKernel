# Test assertion conventions

Mark the phases of every test with `// Arrange`, `// Act`, and `// Assert` comment lines, each once and in that order,
with a blank line before `// Act` and `// Assert`. Omit `// Arrange` when a test needs no setup. Keep the result of the
act in a named local and check it under `// Assert`; an exception assertion that returns the exception belongs under
`// Act`. Use a single `// Act, Assert` line only when one call both acts and asserts and nothing else is checked.
`SKTEST007` reports a complete marker set out of order.

State why an assertion holds in its message instead of a comment. Every `Should*` assertion in
`PandaBear.Testing.Assertions`, `PandaBear.Testing.Http`, and `PandaBear.Testing.Playwright` takes the reason as
`userMessage` after its other arguments and before a trailing `CancellationToken`, and the failure message starts with
it. Pass the values of a `params` assertion as an array to add a message. Name the argument (`userMessage: "..."`) when
the argument before it is also a string. `ShouldBeTrue` and `ShouldBeFalse` take it as an optional argument.
Check an argument exception's parameter name on the exception that `ShouldThrow<T>` returns. Keep
comments for setup and for how an act works.

Use the capability-specific `Should*` APIs so failures describe the assertion that failed.
Keep domain setup local and preserve cancellation, isolation, and cleanup assertions when migrating.

- Use `items.ShouldHaveSingleItem()` or `items.ShouldHaveSingleItem(item => ...)` for uniqueness,
  including retrieval during Arrange or Act. `SKTEST012` reports semantic Enumerable/Queryable
  `Single` calls inside xUnit tests. Deliberately exercising `Single` inside a `ShouldThrow*` or
  xUnit exception assertion is allowed. Async provider methods are outside this rule.
- Use a semantic assertion when available, or name an expression before asserting it. For example,
  retain an equality-operator test as `var areEqual = left == right; areEqual.ShouldBeTrue();`.
  Do not replace operator tests with an assertion that uses different equality semantics.
  `SKTEST013` reports parenthesized `Should*` receivers, including redundant parentheses and awaited
  results. Await the result into a named local first. No automatic semantic assertion is guessed.
  Explicitly typed exception delegates can remain typed arguments to static
  `ShouldAssertionExtensions.ShouldThrow<TException>(...)`, preserving overload selection.
- Never let conditional access skip an assertion. Replace `value?.Title.ShouldBe(expected)` with
  `var actualTitle = value?.Title; actualTitle.ShouldNotBeNull().ShouldBe(expected);`.
  `SKTEST014` reports conditional `ShouldBe*` execution. The code fix supports standalone block
  statements, evaluates the projected receiver once, and fails explicitly when it is null.
  Intentional null expectations require guarding the parent explicitly instead, for example
  `value.ShouldNotBeNull().Title.ShouldBeNull()`; no projected-value fix is offered for those cases.
- Use `await page.ShouldHaveTitle(expectedTitle)` from `PandaBear.Testing.Playwright` for default
  string title assertions. `SKTEST015` reports the equivalent direct Playwright call; a message given to
  `Expect(page, message)` moves to `ShouldHaveTitle(expectedTitle, userMessage)`. Regex titles,
  explicit options, and assertions without a matching repository wrapper remain outside this rule.
  The specialized Playwright implementation may call the native assertion API directly.

The Single fix is offered only when the resulting invocation binds to the repository assertion API
and compiles. Unsupported predicate expression trees, named argument forms, conditional shapes,
and incompatible overloads require a manual migration. Fix All remains unavailable for these fixes.

Strict test names preserve established terms such as `API`, `AsyncAPI`, `HTTP2`, `ValueTask`,
`HttpClient`, `OpenApi`, `ID`, `GUID`, `CSV`, `UI`, `xUnit`, and diagnostic IDs such as `SKTEST004`.
Standalone `Api`/`api` and `AsyncApi` are noncanonical; unrelated words ending in `api` are not
special keywords. Canonical terms are preserved by the underscore-name code fix.

These diagnostics remain in the testing analyzer package; no Playwright dependency is added to
neutral metadata or generic assertions. Production methods and unrelated same-named APIs are not
subject to the new invocation rules. Existing public assertion signatures remain compatible.

Initialize and capture the expected source before registering a test ActivityListener: source construction invokes
existing global listeners, so resolving an uninitialized static source inside the predicate can miss its first activity.
Use exact source identity in predicates, including helper files. Preserve trace or parent
correlation when tests intentionally share a static source. SKTEST010 enforces direct name-based predicates;
SKTEST016 keeps explicitly named SUT construction out of a complete explicit AAA Act block. Neither has an
automatic move/inference fix. SKSTYLE014 requires TimeProvider for wall-clock reads; pass controlled providers
for time-dependent assertions rather than depending on elapsed real time.

## Testing package boundaries

Each testing package owns one kind of helper and takes only the dependencies that kind needs. Add a helper to the package that
already owns its kind; add a package only when a new kind needs a dependency that no existing package may take.

| Package | Owns | May depend on |
| --- | --- | --- |
| `SharedKernel.Testing` | Traits, environment scopes, temporary directories | Test framework only |
| `SharedKernel.Testing.Assertions` | `Should*` assertions for xUnit, `Result` and entity identity | `SharedKernel.Results` |
| `SharedKernel.Testing.Xunit` | `TestScopeAttribute`: typed `Scope` trait for the test seam | `SharedKernel.Testing` |
| `SharedKernel.Testing.Http` | Assertions on HTTP responses and problem details | `SharedKernel.Testing.Assertions` |
| `SharedKernel.Testing.AspNetCore` | Web application factory helpers | ASP.NET Core testing |
| `SharedKernel.Testing.Web` | bUnit element assertions | bUnit |
| `SharedKernel.Testing.Playwright` | Playwright assertions | Playwright |
| `SharedKernel.Testing.Mediator` | Reference mediator test dispatcher | `SharedKernel.Mediator.Abstractions` |
| `SharedKernel.Testing.Data` and `SharedKernel.Testing.Snapshots` | Deterministic data and snapshot text helpers | No SharedKernel package |
| `SharedKernel.Testing.Roslyn` | Compilation, analyzer, and generator test harnesses | Roslyn |
| `SharedKernel.Testing.Analyzers` and `SharedKernel.Testing.CodeFixes` | Analyzers and code fixes for test conventions | Roslyn; code fixes depend on the analyzers |
| `SharedKernel.IntegrationTesting` | Aspire and PostgreSQL integration-test helpers | Aspire testing |
| `SharedKernel.Npgsql.Testing` | PostgreSQL database leases and xUnit fixtures | `SharedKernel.IntegrationTesting` |
| `SharedKernel.Testing.Contracts` | Contract test marker and trait contracts | No SharedKernel package |

Projects under `tests/` that are not packable (`SharedKernel.Testing.Packaging`, `SharedKernel.CodeFixes.Testing`,
`SharedKernel.Testing.Integration`, `SharedKernel.Testing.Scenarios`, `SharedKernel.Testing.System`, and
`SharedKernel.Npgsql.Testing.Host`) serve this repository's own suites and are not part of the public surface. Assertion
packages never depend on a browser, web, or database package, so a consumer takes only the assertions it uses.


## Test project ownership

Every packable project under `src/SharedKernel` has a test project in its reference closure: a project under `tests/` that
references `xunit.v3.mtp-v2` and reaches the source project through `ProjectReference` items. Application-level execution
never substitutes for that coverage. The convention tests in `tests/SharedKernel.Repository.Tests/Conventions` fail when a
packable project has no such test project and no entry in the reviewed exception list of
`SharedKernelTestOwnershipConventionTests`, and when an exception names a project that a test project reaches.

| Kind | Direct owner | Also required |
| --- | --- | --- |
| Library | A `SharedKernel.<Name>.Tests` project that exercises its public behavior | Public API baselines |
| Provider module (PostgreSQL, ClamAV, K6) | A test project that runs against the real provider or its Aspire resource | The shared server rules in [development guidance](development.md#postgresql-test-servers) |
| Analyzer | `SharedKernel.<Name>.Analyzers.Tests` with diagnostic and no-diagnostic cases | Repository convention tests for the package layout |
| Source generator | `SharedKernel.<Name>.GeneratorTests` with snapshot and hint-name assertions | A package-consumption test that builds a fresh project from the packed package |
| Code fix | `SharedKernel.<Name>.CodeFixes.Tests` with before and after sources | Code-fix runner coverage where the fix ships in a testing package |
| Package-consumption test | A test project such as `SharedKernel.Mediator.PackageConsumptionTests` that restores packed packages from a local feed | No CI lane unless its runtime justifies one |

A generator that ships inside another package, such as the results generator inside `PandaBear.SharedKernel.Results`, is not
packable and keeps its own `GeneratorTests` project. A packable project with no test project in its reference closure needs a
reviewed exception, with its reason beside the exception in `SharedKernelTestOwnershipConventionTests`.
