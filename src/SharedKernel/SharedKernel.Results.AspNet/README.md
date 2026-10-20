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
