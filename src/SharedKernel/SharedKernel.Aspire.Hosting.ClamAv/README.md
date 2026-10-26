# SharedKernel.Aspire.Hosting.ClamAv

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration for a private ClamAV container.

## Usage

```csharp
var clamAv = builder.AddClamAv("clamav")
                    .WithDataVolume();

builder.AddProject<Projects.Api>("api")
       .WithClamAvReference(clamAv)
       .WaitFor(clamAv);
```

The container exposes `clamd` on a private TCP endpoint and is healthy once it answers `PING`. Without `WithDataVolume` or
`WithDataBindMount` the virus definitions live and die with the container. Use `WithFreshClam(false)` when the definitions come
from the image or another update process. The image is pinned by tag and digest.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.ClamAv`. C# namespace: `PandaBear.Aspire.Hosting.ClamAv`. Assembly/project identity: `SharedKernel.Aspire.Hosting.ClamAv`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
