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
`ShouldThrow<T>()` without a parameter name takes no message, because its string overload checks `paramName`. Keep
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
