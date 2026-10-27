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

### Filer UI (local development only)

`WithFilerUi()` exposes the Filer UI that `weed mini` already serves on port `8888` as an external `filer-ui` endpoint with a
dashboard link. The UI has no authentication and allows reading and changing every stored object, so use it only on a
development machine. The endpoint is added in run mode only; publish manifests and resources that never call the method keep
every endpoint internal.

The resource waits on the master (`/cluster/healthz`) and the S3 gateway (`/healthz`). `WithAccessKey` and `WithSecretKey` replace the generated credentials with parameters. `AddBucket(name, bucketName)` adds a child bucket resource whose connection string carries the S3 endpoint and bucket name, never the credentials; the container creates every bucket at startup through `S3_BUCKET` and skips existing ones.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.SeaweedFs`. C# namespace: `PandaBear.Aspire.Hosting.SeaweedFs`. Assembly/project identity: `SharedKernel.Aspire.Hosting.SeaweedFs`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
