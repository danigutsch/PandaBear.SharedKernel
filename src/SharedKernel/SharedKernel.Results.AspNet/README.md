# PandaBear.SharedKernel.Results.AspNet

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Map transport-neutral result statuses to conventional ASP.NET Core HTTP status codes without placing HTTP dependencies in the Results contract package. Use the mapping at an endpoint boundary; application policy still owns the response body, problem details, and authorization. Undefined statuses throw rather than silently selecting a success code.

## Install

Replace both placeholders with an actually validated candidate version and its private feed; the placeholders do not identify a published package version.

```text
dotnet add package PandaBear.SharedKernel.Results.AspNet --version <validated-candidate-version> --source <private-candidate-feed>
```

## Example

```csharp
using PandaBear.Results;
using PandaBear.Results.AspNet;

public static class Example
{
    public static int MissingItemStatus() => ResultStatus.NotFound.ToHttpStatusCode();
}
```

## Constraints

Check success/failure before accessing payloads; propagate cancellation in asynchronous flows. Keep business validation in its owning domain. Registry publication remains deferred.

Package ID: `PandaBear.SharedKernel.Results.AspNet`. C# namespace: `PandaBear.Results.AspNet`. Assembly/project identity: `SharedKernel.Results.AspNet`.

## Minimal API responses

`result.ToHttpResult()` returns 204 for Ok and NoContent and 202 for Accepted. `Result<T>.ToHttpResult()` returns 200 with the value for Ok,
201 with the value for Created, and 202 with the value for Accepted. A Created response sets no `Location` header; set one in the
endpoint when you need it. A failure becomes a validation problem (invalid results, with their field errors) or a problem response
with the mapped status code.

- A failure that maps to HTTP 5xx (Error, CriticalError, Unavailable) never sends the result's own detail to the client, because it may
  hold internal text such as exception messages, SQL or paths. The response carries a fixed detail instead:
  `ResultHttpExtensions.InternalServerErrorDetail` ("An unexpected error occurred.") for 500 and
  `ResultHttpExtensions.ServiceUnavailableDetail` ("The service is temporarily unavailable.") for 503. Log the real detail yourself.
- A failure that maps to 4xx (Invalid, Unauthorized, Forbidden, NotFound, Conflict) sends the result's detail, so keep those details
  safe for callers to read.
- An uninitialized result (`default(Result)` or `default(Result<T>)`) throws `InvalidOperationException` instead of becoming a response.

## Optimistic concurrency over HTTP

`ConcurrencyHeaders` carries a record's numeric version as a strong entity tag. `ETag(version)` formats `"3"`. `ReadVersion(request)`
reads the version an update is based on from `If-Match` and returns an invalid result keyed to `version` when the header is missing, `*`, a
weak tag (`W/"3"`), a list of tags, or not a non-negative integer, because RFC 9110 requires strong comparison for `If-Match`. A stale
version is not detected here: the update reports it (for example as a Conflict result, HTTP 409). `Update(request, send)` reads the
version, skips `send` when it is unusable, and converts the outcome with `ToHttpResult()`.

```csharp
group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
    (await sender.Send(new GetThing(id), ct)).ToHttpResult(thing => thing.Version));
group.MapPut("/{id:guid}", (Guid id, ThingBody body, HttpRequest request, ISender sender, CancellationToken ct) =>
    ConcurrencyHeaders.Update(request, version => sender.Send(new UpdateThing(id, body, version), ct)));
group.MapPost("/", async (ThingBody body, ISender sender, CancellationToken ct) =>
    (await sender.Send(new CreateThing(body), ct)).ToCreatedHttpResult(id => $"/api/v1/things/{id}", id => new { id }));
```

`ToHttpResult(version)` sets the `ETag` header on a successful read. `ToCreatedHttpResult(location, body)` answers 201 with a `Location`
header and the body you choose, and the usual problem response for a failure.
