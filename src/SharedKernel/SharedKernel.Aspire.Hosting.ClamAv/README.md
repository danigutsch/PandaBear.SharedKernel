# SharedKernel.Aspire.Hosting.ClamAv

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable .NET Aspire hosting extensions for a private local ClamAV daemon.

The package adds a pinned `clamav/clamav` container resource, an internal TCP
endpoint for `clamd`, virus definitions that persist between AppHost runs, and
a PING/PONG health check.

The definitions live in the named volume `{name}-definitions`, so a restarted
AppHost reuses them. When the `DcpPublisher:ResourceNameSuffix` configuration
value is set, the resource is an isolated instance, such as a parallel test
run. It then gets an anonymous volume that the container runtime removes
together with the container, because the named volume of an isolated instance
is never reused and would stay behind after every run.

FreshClam is enabled by default. Use `.WithFreshClam(false)` only when virus
definitions are supplied by the image, a mounted volume, or another update
process.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.ClamAv`. C# namespace: `PandaBear.Aspire.Hosting.ClamAv`. Assembly/project identity: `SharedKernel.Aspire.Hosting.ClamAv`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
