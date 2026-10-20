# SharedKernel.Aspire.Hosting.SeaweedFs

Reusable .NET Aspire hosting extensions for authenticated local SeaweedFS S3
storage.

The package adds a pinned `chrislusf/seaweedfs` container resource, internal S3
and master endpoints, persistent `/data` storage, and a master health check.

S3 credentials are supplied through Aspire parameters and passed to the server
container with `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, so unsigned S3
requests are rejected.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.SeaweedFs`. C# namespace: `PandaBear.Aspire.Hosting.SeaweedFs`. Assembly/project identity: `SharedKernel.Aspire.Hosting.SeaweedFs`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
