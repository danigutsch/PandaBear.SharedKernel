# SharedKernel.Aspire.Hosting.SeaweedFs

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable .NET Aspire hosting extensions for authenticated local SeaweedFS S3
storage.

The package adds a pinned `chrislusf/seaweedfs` container resource, internal S3
and master endpoints, `/data` storage that persists between AppHost runs, and a
master health check.

The data lives in the named volume `{name}-data`, so a restarted AppHost reuses
it. When the `DcpPublisher:ResourceNameSuffix` configuration value is set, the
resource is an isolated instance, such as a parallel test run. It then gets an
anonymous volume that the container runtime removes together with the
container, because the named volume of an isolated instance is never reused and
would stay behind after every run.

S3 credentials are supplied through Aspire parameters and passed to the server
container with `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, so unsigned S3
requests are rejected.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.SeaweedFs`. C# namespace: `PandaBear.Aspire.Hosting.SeaweedFs`. Assembly/project identity: `SharedKernel.Aspire.Hosting.SeaweedFs`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
