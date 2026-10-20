# SharedKernel.Aspire.Hosting.ClamAv

Reusable .NET Aspire hosting extensions for a private local ClamAV daemon.

The package adds a pinned `clamav/clamav` container resource, an internal TCP
endpoint for `clamd`, persistent virus definitions, and a PING/PONG health
check.

FreshClam is enabled by default. Use `.WithFreshClam(false)` only when virus
definitions are supplied by the image, a mounted volume, or another update
process.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.ClamAv`. C# namespace: `PandaBear.Aspire.Hosting.ClamAv`. Assembly/project identity: `SharedKernel.Aspire.Hosting.ClamAv`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
