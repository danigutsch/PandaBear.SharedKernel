# SharedKernel.Aspire.Hosting.SeaweedFs

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration for a SeaweedFS container with an authenticated S3-compatible API.

## Usage

```csharp
var storage = builder.AddSeaweedFs("seaweedfs", bucketDefault: "media")
                     .WithDataVolume();
```

The S3 credentials are the secret parameters `{name}-access-key` and `{name}-secret-key`, so unsigned requests are rejected.
Without `WithDataVolume` or `WithDataBindMount` the stored objects live and die with the container. The image is pinned by tag
and digest.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.SeaweedFs`. C# namespace: `PandaBear.Aspire.Hosting.SeaweedFs`. Assembly/project identity: `SharedKernel.Aspire.Hosting.SeaweedFs`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
