# SharedKernel.Aspire.Hosting.ClamAv

Reusable .NET Aspire hosting extensions for a private local ClamAV daemon.

The package adds a pinned `clamav/clamav` container resource, an internal TCP
endpoint for `clamd`, persistent virus definitions, and a PING/PONG health
check.

FreshClam is enabled by default. Use `.WithFreshClam(false)` only when virus
definitions are supplied by the image, a mounted volume, or another update
process.
