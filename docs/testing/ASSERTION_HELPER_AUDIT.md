# Assertion helper audit

This audit lists multi-statement assertion patterns that repeat across the SharedKernel test projects and could be expressed in one call by a `Should*` helper in `SharedKernel.Testing.Assertions` or its Http, Web, Data, Mediator and Logging siblings. It proposes helpers only. Nothing here is implemented, and no helper has been accepted yet.

## Method

Counts come from `grep -rE` over `tests/**/*.cs`, excluding `obj` and `bin`, at the commit this audit was written against. "Lines" is the number of matching lines and "files" is the number of distinct files. Counts are approximate where a pattern can match unrelated code, and each entry says so when that applies. The `catch` blocks that appear in `SharedKernel.Style.Analyzers.Tests` are analyzer input strings, not assertions, and are excluded.

The existing surface was checked first so that the list does not propose what already exists. `ShouldThrow`, `ShouldThrowAssignableTo`, `ShouldThrowInner`, `ShouldHaveCount`, `ShouldHaveSingleItem`, `ShouldAllSatisfy`, `ShouldBeSuccess`, `ShouldBeFailure`, `ShouldBeProblem` and the string and collection assertions all exist today.

## Ranked candidates

Ranking weighs duplicate count first, then how many lines each call site saves, then how much failure-message quality improves over the current form.

### 1. Exception message and parameter name

Tests capture an exception with `ShouldThrow<T>()` and then assert on `Message` or `ParamName` in separate statements.

- `Message.Should*`: 390 lines in 97 files. Examples: `SharedKernel.ImageProcessing.Tests/MagickImageProcessorTests.cs:201`, `SharedKernel.Domain.Tests/CompositeDomainEventDispatcherTests.cs:79`.
- `ParamName.ShouldBe`: 111 lines in 41 files. Examples: `SharedKernel.EventSourcing.Npgsql.Tests/PostgreSqlTestDatabaseLeaseTests.cs:21`, `SharedKernel.EventSourcing.Npgsql.Tests/PostgreSqlProjectionCheckpointStoreTests.cs:66`.

Candidate: overloads such as `ShouldThrow<T>(messageContains)` and `ShouldThrowArgument<T>(paramName)` that assert the type and the detail in one call and report all three values on failure. This is the largest group, and the `ParamName` half is the most uniform. The `Message` half varies between exact match, `Contains` and `StartsWith`, so a single overload may not cover it. Risk: an overload that hides the exception makes tests that inspect further members, such as `InnerException`, still need the captured value, so the helper must keep returning it.

### 2. Filesystem existence

Tests assert `File.Exists(path)` or `Directory.Exists(path)` through `ShouldBeTrue` or `ShouldBeFalse`.

- 144 lines in 50 files. Of those, 47 wrap `Path.Combine` inside `File.Exists`. Examples: `SharedKernel.Aspire.Hosting.K6.Tests/K6ScenarioResourceExtensionsTests.cs:60`, `SharedKernel.Repository.Tests/SliceServiceTests.cs:134`, `SharedKernel.Style.Analyzers.Tests/GovernedLiteralRepositoryTests.cs:33`.
- Related reads: `File.ReadAllText`, `ReadAllLines` or `ReadAllBytes` followed by a string assertion, 20 lines in 9 files (`K6ScenarioResourceExtensionsTests.cs:103`).

Candidate: `ShouldExistAsFile`, `ShouldExistAsDirectory` and a negative form, plus optionally `ShouldHaveFileContent`. The current form fails with "Expected True, got False" and does not name the path, so the gain is mostly in failure messages. These would live in `SharedKernel.Testing.Assertions` and need no new dependency.

### 3. JSON property reads

Tests parse a response or manifest and assert on `RootElement.GetProperty("x").GetString()` style chains.

- Typed getter followed by `ShouldBe`: 101 lines in 21 files. All `GetProperty` chains: 182 lines in 26 files. Examples: `SharedKernel.AspNetCore.Tests/UnhandledExceptionHandlerTests.cs:29`, `SharedKernel.Repository.Tests/SliceServiceTests.cs:313`, `SharedKernel.Repository.Tests/RepositoryValidationTests.cs:190`.

Candidate: `JsonElement` extensions such as `ShouldHaveString(name, expected)`, `ShouldHaveInt32(name, expected)` and `ShouldHaveProperty(name)` returning the element. A missing property currently throws `KeyNotFoundException` from inside the chain with no assertion context. Most of the volume is in `SharedKernel.Repository.Tests`, so this may fit that project's local helpers better than a shared package; check whether other projects would adopt it before promoting it.

### 4. Metric measurement tags

Telemetry tests read `measurement.Tags[...]` repeatedly for one measurement.

- 31 `Tags[...].ShouldBe` lines in 4 files: `SecurityEventPipelineTests.cs` (8), `SecurityEventTelemetryTests.cs` (10), `UnhandledExceptionHandlerTests.cs` (8) and `UiFailureReporterTests.cs` (5). Example: `SharedKernel.AspNetCore.Tests/SecurityEventTelemetryTests.cs:42`.

Candidate: a tag-set assertion such as `ShouldHaveTags(("event", "authentication_failed"), ("reason", reason))` on the measurement type. The count is modest and the helper would need to depend on whichever measurement type these tests share, so confirm that type is public before proposing it for a package.

### 5. Dictionary key presence

- `ContainsKey(...).ShouldBeTrue/False`: 75 lines in 12 files (54 negative, 21 positive). Examples: `SharedKernel.Aspire.Hosting.K6.Tests/K6ScenarioResourceExtensionsTests.cs:324`, `SharedKernel.Repository.Tests/ServiceHostTests.cs:43`.

Candidate: `ShouldContainKey` and `ShouldNotContainKey`, optionally returning the value. The existing `ShouldContain` overloads are for sequences and strings and do not cover dictionaries.

### 6. Stream length and position

- `Length.ShouldBe` and `Position.ShouldBe`: 113 lines in 53 files, but many are `FileInfo.Length`, byte arrays or checkpoint positions that are not streams. A stream-only count was not isolated. Example: `SharedKernel.IO.Tests/ReadOnlyMemoryStreamTests.cs:26`.

Candidate: low priority. A stream assertion would only pay off in the IO-related projects, and a precise count is needed before accepting it.

## Migration to existing helpers

These are not new helpers. They are existing helpers that tests have not adopted yet, and replacing them needs no API change.

- `Count.ShouldBe` and `Length.ShouldBe` on collections: 105 and 72 lines. `ShouldHaveCount` is used 26 times. Part of the `Length` count is `FileInfo.Length` or arrays used for something other than size, so the real migration count is lower. `ShouldHaveCount` takes `IEnumerable<T>`, so arrays and lists are covered.
- Manual `IsSuccess` and `IsFailure` assertions: 64 lines in 8 files, concentrated in `SharedKernel.Results.Tests`. `ShouldBeSuccess` and `ShouldBeFailure` exist. These tests may assert on the `Result` type itself rather than on a consumer, so the manual form can be intentional there.
- `foreach` loops that assert in the body: the raw `foreach` count is 142 lines in 68 files, but most are not assertion loops. `ShouldAllSatisfy` exists and is used; a targeted review would be needed to count real candidates.

## Not proposed

- Try and catch blocks asserting exceptions. Real instances in test classes are rare once analyzer input strings are excluded, and `ShouldThrow` already covers the pattern.
- Log record checks. Only 22 lines in 10 files touch the recorded log surface, and `RecordedLog` already exposes the data directly.
- HTTP status code checks. `StatusCode.ShouldBe` appears 31 times in 11 files, and the 400 and problem-response cases are already covered by `ShouldBeProblem`. The remaining plain status checks are one line each and do not justify a helper.

## Next steps

Each accepted candidate should become its own issue or a batch under one pull request, with its own tests and with failure-message quality at least equal to the form it replaces. Candidates 1 and 2 are the strongest. Candidate 3 needs a decision about whether it belongs in a shared package or in `SharedKernel.Repository.Tests`.
