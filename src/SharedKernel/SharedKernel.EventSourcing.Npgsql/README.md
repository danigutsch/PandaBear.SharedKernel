# SharedKernel.EventSourcing.Npgsql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Raw Npgsql adapter for the storage-neutral `SharedKernel.EventSourcing` abstractions.

This package owns reusable event-stream and projection-checkpoint persistence. Bounded contexts still
own composition, schema naming, migrations/initialization policy, and context-specific read models.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EventSourcing.Npgsql`. C# namespace: `PandaBear.EventSourcing.Npgsql`. Assembly/project identity: `SharedKernel.EventSourcing.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
