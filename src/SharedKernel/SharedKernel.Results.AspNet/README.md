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
