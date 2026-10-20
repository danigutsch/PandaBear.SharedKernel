# SharedKernel.Aspire.Hosting.ClamAv

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable .NET Aspire hosting extensions for a private local ClamAV daemon.

The package adds a pinned `clamav/clamav` container resource, an internal TCP
endpoint for `clamd`, persistent virus definitions, and a PING/PONG health
check.

FreshClam is enabled by default. Use `.WithFreshClam(false)` only when virus
definitions are supplied by the image, a mounted volume, or another update
process.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.ClamAv`. C# namespace: `PandaBear.Aspire.Hosting.ClamAv`. Assembly/project identity: `SharedKernel.Aspire.Hosting.ClamAv`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
