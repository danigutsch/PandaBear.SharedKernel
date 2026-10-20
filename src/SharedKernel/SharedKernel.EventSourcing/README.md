# SharedKernel.EventSourcing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Storage-neutral event-sourcing primitives for aggregates, append/load operations, stream versions, and projections.

This project intentionally contains abstractions only. Reusable provider implementations belong in
adapter packages such as `SharedKernel.EventSourcing.Npgsql`; bounded-context infrastructure owns
composition, schema names, migrations, read models, and context-specific projection rebuild policy.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EventSourcing`. C# namespace: `PandaBear.EventSourcing`. Assembly/project identity: `SharedKernel.EventSourcing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
