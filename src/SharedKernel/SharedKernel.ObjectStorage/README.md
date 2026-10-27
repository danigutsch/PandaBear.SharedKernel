# SharedKernel.ObjectStorage

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Host-neutral object-storage contracts.

`IObjectStore` writes, reads, lists, deletes, and signs direct uploads for application-owned, relative,
slash-delimited object keys. It carries no application key policy, variants, or publishing rules.

Provider implementations belong in separate packages. The S3-compatible provider is
`SharedKernel.ObjectStorage.S3`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ObjectStorage`. C# namespace: `PandaBear.ObjectStorage`. Assembly/project identity: `SharedKernel.ObjectStorage`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
