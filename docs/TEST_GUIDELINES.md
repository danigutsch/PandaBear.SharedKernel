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

Strict test names write listed terms in their listed spelling, such as `API`, `AsyncAPI`, `HTTP2`, `JSON`, `GUID`,
`ID`, `UI`, `ValueTask`, `HttpClient`, `DbContext`, `xUnit`, and diagnostic IDs such as `SKTEST004`: `json` or `Json`
as a whole segment is reported, while words that merely contain a term, such as `main`, are not. A repository adds its
own terms with `sharedkernel_testing_additional_allowed_segments`. The underscore-name code fix applies the listed
spelling. See the [testing analyzer README](../src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#listed-segments)
for the full list and rules.

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

## Captured output

Capture the text a command-line tool, a console or any other `TextWriter` consumer writes with
`PandaBear.Testing.InvariantStringWriter`. It ends each line with `"\n"` and formats with the invariant culture, so the
captured text is the same on Linux, macOS and Windows and an expectation is written once with `"\n"`. A plain
`StringWriter` ends lines with `Environment.NewLine`, so a `"\n"` expectation passes on Linux and fails only on Windows.
Write expectations for captured lines with `"\n"`, not `Environment.NewLine`; keep `Environment.NewLine` only where the
code under test writes it explicitly, or for the output of a real child process. A test that needs another line ending,
such as one that checks a writer passes on its target's line ending, sets it in the initializer:
`new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" }`. `SKTEST022` reports every other
`new StringWriter(...)` in a test project, and its code fix replaces `new StringWriter()` and
`new StringWriter(CultureInfo.InvariantCulture)` with `new InvariantStringWriter()`.

## Log output

Code under test logs to the output of the test it runs for, never to the process console. `dotnet test` (.NET 11 SDK)
streams what a test process writes to its console for every test, passing or not, while the runner shows a test's own
output only when the test fails. `PandaBear.Testing.TestOutputLoggerProvider` writes each entry to the test it belongs to
(the test running where it is logged, else the test that created the provider); `WebApplicationTestHost`,
`KestrelTestHost` and `AspireTestApplication` use it in place of the console logger. Add it to a service collection a test
builds itself: `services.AddLogging(logging => logging.AddProvider(new TestOutputLoggerProvider()))`.

## Testing package boundaries

Each testing package owns one kind of helper and takes only the dependencies that kind needs. Add a helper to the package that
already owns its kind; add a package only when a new kind needs a dependency that no existing package may take.

| Package | Owns | May depend on |
| --- | --- | --- |
| `SharedKernel.Testing` | Traits, environment scopes, temporary directories, stream and executable doubles, file-system probes | Test framework only |
| `SharedKernel.Testing.Assertions` | `Should*` assertions for xUnit, `Result` and entity identity | `SharedKernel.Results` |
| `SharedKernel.Testing.Xunit` | Typed trait attributes (`TestScope`, `TestHost`, `TestFocus`, `TestPriority`, `TestArea`, `TestSurface`), `TestOutputLoggerProvider` | `SharedKernel.Testing`, xUnit |
| `SharedKernel.Testing.Http` | Assertions on HTTP responses and problem details, delegate handler, tracking content | `SharedKernel.Testing.Assertions` |
| `SharedKernel.Testing.EntityFrameworkCore` | Minimal `TestDbContext` and `TestEntity` | `Microsoft.EntityFrameworkCore.Relational` |
| `SharedKernel.Testing.Sqlite` | Private in-memory SQLite database per test | `Microsoft.Data.Sqlite.Core` |
| `SharedKernel.Testing.Logging` | Recording and throwing `ILogger` fakes | `Microsoft.Extensions.Logging.Abstractions` |
| `SharedKernel.Testing.AI` | Fixed-result and failing `IImageTextGenerator` with request capture | `SharedKernel.AI` |
| `SharedKernel.Testing.MalwareScanning` | Clean, infected, throwing, blocking, and recording `IMalwareScanner` fakes | `SharedKernel.MalwareScanning` |
| `SharedKernel.Testing.AspNetCore` | Web application factory helpers, form-file, browser-file and throwing-cache fakes | ASP.NET Core testing, `SharedKernel.Testing.Xunit` |
| `SharedKernel.Testing.Web` | bUnit element assertions | bUnit |
| `SharedKernel.Testing.Playwright` | Playwright assertions | Playwright |
| `SharedKernel.Testing.Domain` | Domain event dispatcher and dispatch handler fakes | `SharedKernel.Domain` |
| `SharedKernel.Testing.Mediator` | Reference mediator test dispatcher | `SharedKernel.Mediator.Abstractions` |
| `SharedKernel.Testing.Messaging` | Event envelope publisher and integration event handler fakes | `SharedKernel.Messaging`, `SharedKernel.Messaging.IntegrationEvents` |
| `SharedKernel.Testing.ObjectStorage` | In-memory object store with call recording and failure injection | `SharedKernel.ObjectStorage` |
| `SharedKernel.Testing.Data` and `SharedKernel.Testing.Snapshots` | Deterministic data and snapshot text helpers | No SharedKernel package |
| `SharedKernel.Testing.Roslyn` | Compilation, analyzer, generator, and code fix test harnesses | Roslyn, Roslyn Workspaces |
| `SharedKernel.Testing.Analyzers` and `SharedKernel.Testing.CodeFixes` | Analyzers and code fixes for test conventions | Roslyn; code fixes depend on the analyzers |
| `SharedKernel.IntegrationTesting` | Aspire and PostgreSQL integration-test helpers | Aspire testing, `SharedKernel.Testing.Xunit` |
| `SharedKernel.Npgsql.Testing` | PostgreSQL database leases and xUnit fixtures | `SharedKernel.IntegrationTesting` |
| `SharedKernel.Testing.Contracts` | Contract test marker and trait contracts | No SharedKernel package |

Projects under `tests/` that are not packable (`SharedKernel.Testing.Packaging`,
`SharedKernel.Testing.Integration`, `SharedKernel.Testing.Scenarios`, `SharedKernel.Testing.System`,
`SharedKernel.Aspire.Hosting.Umami.Testing`, and `SharedKernel.TestServices.Host`) serve this repository's own suites and are not part of the public surface. Assertion
packages never depend on a browser, web, or database package, so a consumer takes only the assertions it uses.


## Test project ownership

Every packable project under `src/SharedKernel` has a test project in its reference closure: a project under `tests/` that
references `xunit.v3.mtp-v2` and reaches the source project through `ProjectReference` items. Application-level execution
never substitutes for that coverage. The convention tests in `tests/SharedKernel.Repository.Tests/Conventions` fail when a
packable project has no such test project and no entry in the reviewed exception list of
`TestOwnershipConventionTests`, and when an exception names a project that a test project reaches.

| Kind | Direct owner | Also required |
| --- | --- | --- |
| Library | A `SharedKernel.<Name>.Tests` project that exercises its public behavior | Public API baselines |
| Provider module (PostgreSQL, ClamAV, K6) | A test project that runs against the real provider or its Aspire resource | The shared service rules in [development guidance](development.md#shared-test-services) |
| Analyzer | `SharedKernel.<Name>.Analyzers.Tests` with diagnostic and no-diagnostic cases | Repository convention tests for the package layout |
| Source generator | `SharedKernel.<Name>.GeneratorTests` with snapshot and hint-name assertions | A package-consumption test that builds a fresh project from the packed package |
| Code fix | `SharedKernel.<Name>.CodeFixes.Tests` with before and after sources | Code-fix runner coverage where the fix ships in a testing package |
| Package-consumption test | A test project such as `SharedKernel.Mediator.PackageConsumptionTests` that restores packed packages from a local feed | No CI lane unless its runtime justifies one |

A generator that ships inside another package, such as the results generator inside `PandaBear.SharedKernel.Results`, is not
packable and keeps its own `GeneratorTests` project. A packable project with no test project in its reference closure needs a
reviewed exception, with its reason beside the exception in `TestOwnershipConventionTests`.
