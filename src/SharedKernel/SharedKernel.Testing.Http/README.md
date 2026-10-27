# SharedKernel.Testing.Http

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable HTTP response and message-handler helpers for SharedKernel test suites.

## Usage

Construct `HttpClient` with `TestHttpMessageHandler`, enqueue HTTP or JSON responses, and inspect its captured requests. `HttpResponseFactory.Json` creates explicit test responses. This test handler avoids network access; consumers retain response-disposal and contract assertions.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Http`. C# namespace: `PandaBear.Testing.Http`. Assembly/project identity: `SharedKernel.Testing.Http`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Problem responses

```csharp
using PandaBear.Testing.Http;

var problem = await response.ShouldBeValidationProblem(
    expectedDetail: "invalid id",
    expectedCode: "invalid",
    cancellationToken: cancellationToken);
problem.ShouldHaveValidationError("Id", "Id is required.");
problem.ShouldHaveValidationErrors(new Dictionary<string, string[]>
{
    ["Id"] = ["Id is required."]
});
```

`ShouldBeValidationProblem` checks HTTP 400, `application/problem+json`, body status, and a
nonempty errors object containing nonempty message arrays. Detail and top-level `code` are checked
only when supplied. A missing expected code fails; the helper never substitutes a detail for a code.
`ShouldHaveValidationError` allows other fields; `ShouldHaveValidationErrors` rejects extra fields.
Field names and messages are case-sensitive, and message order and multiplicity must match.

For other statuses, use `await response.ShouldBeProblem(HttpStatusCode.NotFound,
expectedCode: "not_found", cancellationToken: cancellationToken)`. Helpers return detached
`JsonElement` values, avoid reflection-based serialization, and leave response ownership with
the caller. Title, type, instance, privacy headers, and application-specific extensions remain
available for explicit assertions.

Each assertion also has an overload that takes `userMessage`, the reason the test expects it to
hold. The `ShouldBeProblem` and `ShouldBeValidationProblem` message overloads take `expectedDetail`
and `expectedCode` explicitly (pass `null` to leave one unchecked), then `userMessage`, then the
optional token:

```csharp
await response.ShouldBeProblem(HttpStatusCode.NotFound, null, "not_found",
    userMessage: "Deleted orders stay deleted.", cancellationToken: cancellationToken);
```

A message overload reports a failure as an `XunitException` whose message starts with
`userMessage` and whose inner exception is the original failure. Argument errors and cancellation
pass through unchanged.

## Hypermedia links

Assert the `links` array of a parsed payload with `JsonElement` helpers:

```csharp
var link = payload.ShouldHaveLink("self");   // object payload; returns the matching link
collection.ShouldAllHaveLink("self");        // nonempty array payload; every item needs the relation
```

`ShouldHaveLink` requires a `links` array containing an object whose `rel` equals the relation
(case-sensitive) and whose `href` is a nonblank string. `ShouldAllHaveLink` fails on an empty array.
Route values and other relations stay in the consuming test. Both have `userMessage` overloads.
