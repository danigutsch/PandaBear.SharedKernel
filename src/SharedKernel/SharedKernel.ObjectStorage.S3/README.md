# SharedKernel.ObjectStorage.S3

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

S3-compatible provider for `SharedKernel.ObjectStorage`. It works with AWS S3, Cloudflare R2, and
S3-compatible servers such as SeaweedFS.

`AddS3ObjectStore(configure)` populates `S3ObjectStoreOptions` (bind a configuration section in the callback), validates them on start, and registers
`IObjectStore`. Supply `AccessKey` and `SecretKey` through a secret store. Pre-provision the private
bucket in production; `AutoProvisionBucket` is for local development only. `PublicBaseUri` is
independent of the authenticated endpoint. Presigned PUT tickets sign `Content-Type` and the exact
`Content-Length`, and last at most seven days.

Traces come from the `SharedKernel.ObjectStorage.S3` activity source and carry only the operation name; bucket, object
key, URL and request identifiers are never tagged. AWS SDK tracing is not registered, and HttpClient instrumentation is
suppressed while an operation runs, so path-style URLs cannot reach exporters.
Hosting a local SeaweedFS container belongs to `SharedKernel.Aspire.Hosting.SeaweedFs`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ObjectStorage.S3`. C# namespace: `PandaBear.ObjectStorage.S3`. Assembly/project identity: `SharedKernel.ObjectStorage.S3`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
