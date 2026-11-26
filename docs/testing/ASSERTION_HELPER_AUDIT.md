# Assertion helper audit

This audit ranks repeated test assertions and records which ones justify a
shared helper. It covers the base assertion package and the HTTP, Web, Data,
Mediator and Logging capabilities.

## Method

The inventory is anchored to commit `026d45d98912760e5f8545c05cc7689ff86138a7`.
Use `git grep -E -c '<pattern>' <revision> -- 'tests/**/*.cs'` to reproduce
its lexical counts. The numbers are matching lines and distinct tracked
files. A multiline chain can be missed, and a string literal or unrelated
operation can match. The call sites cited below were read in the source.

Existing assertion coverage includes `ShouldThrow`,
`ShouldThrowAssignableTo`, `ShouldThrowInner`, `ShouldHaveCount`,
`ShouldHaveSingleItem`, `ShouldAllSatisfy`, `ShouldBeSuccess`,
`ShouldBeFailure` and `ShouldBeProblem`. Preserve useful failure messages
and return values when considering a wrapper.

## Ranked candidates

### 1. Path existence

`File.Exists…ShouldBeTrue/False` matches 152 lines across 72 files.
`Directory.Exists…ShouldBeTrue/False` matches 111 lines across 38 files.
Accept a batch of four type-specific path assertions. A Boolean failure
today omits the path and expected entry type. The APIs improve diagnostics
while preserving the predicates for valid paths.

### 2. Exceptions

`.Message.ShouldBe/Contain/Start` matches 707 lines across 216 files.
`.ParamName.ShouldBe(` matches 365 lines across 137 files.
Defer. Retain the returned exception. Message expectations vary by exact
value, substring and prefix. A dedicated argument helper needs a consistent
name, overloads and async usage.

### 3. JSON properties

Typed JSON property getters followed by `ShouldBe` match 186 lines across
37 files. Defer. Missing-property diagnostics could improve, but typed
reads, nullability and array checks need distinct semantics. Keep the
current repository fixture calls while judging cross-package need.

### 4. Dictionary keys

`ContainsKey` plus a Boolean assertion matches 112 lines across 31 files.
Defer. A wrapper needs to account for the dictionary's comparer and
separate presence assertions from returning a stored value.

### 5. Metric tags

A metric tag indexer followed by `ShouldBe` matches 42 lines across eight
files. Defer. Keep the metric carrier owner and decide whether a tag helper
checks a subset, duplicate keys or the full captured set.

### 6. Stream length and position

`Length` or `Position` followed by `ShouldBe` matches 159 lines across 86
files. Defer. This upper-bound count includes arrays, files and checkpoint
positions. Stream-specific assertions require a separate count and a
concrete contract.

For exception examples, see `CandidateManifestTests` in Versioning.Tests
and `PostgreSqlProjectionCheckpointStoreTests` in EventSourcing.Npgsql.Tests.
`CoverageGateTests` in Repository.Tests contains example JSON calls.
`K6ScenarioResourceExtensionsTests` inspects dictionary keys, and
`SecurityEventTelemetryTests` checks values on individual metric tags.

## Accepted filesystem batch

Use `ShouldExistAsFile`, `ShouldNotExistAsFile`, `ShouldExistAsDirectory`
and `ShouldNotExistAsDirectory` from `SharedKernel.Testing.Assertions`.
The two absence assertions answer whether their requested entry type is
present. Either accepts a missing path, and each accepts an entry of the
opposite type. Well-formed paths use the platform predicate, including its
treatment of inaccessible paths and symbolic links. Empty and whitespace
paths give a parameter error. Failures include the original path and the
expected entry type, with a caller reason first when supplied.

`CodeFixRunEngineTests.Cancellation_during_membership_evaluation_removes_the_staged_helper`
is the representative adoption. It checks that the staged helper appears
during evaluation and disappears after cancellation. Dedicated controls
also exercise real directories, missing paths and failing assertions.

## Keep the existing assertions

- `IsSuccess` and `IsFailure` followed by a Boolean assertion match 212
  lines across 49 files. Consumer tests can use `ShouldBeSuccess` and
  `ShouldBeFailure` where applicable. Tests of the result type itself
  can keep assertions on those properties as part of its contract.
- `StatusCode.ShouldBe` matches 138 lines across 35 files. The status
  comparisons remain concise. Use `ShouldBeProblem` for problem responses.
- Use `ShouldAllSatisfy` for loops consisting solely of assertions.
  Ordinary test loops also arrange and act, so their count is outside
  this lexical survey.
- Use `ShouldThrow` for manual exception capture where the assertion
  preserves the intended exception type and outcome. Analyzer fixture
  snippets contain `try` and `catch` tokens and make a lexical count
  unreliable.
- Recorded logs already expose the rendered message, captured state and
  exception independently. Preserve those distinctions in assertions.

This completes the ranked audit and records the first chosen batch for
[issue 255](https://github.com/danigutsch/PandaBear.SharedKernel/issues/255).
