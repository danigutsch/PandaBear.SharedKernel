# SharedKernel.Domain

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Persistence-agnostic DDD primitives shared by bounded contexts.

The package also owns typed in-process domain-event dispatch contracts. Generated modules contribute
closed `IDomainEventDispatchHandler` implementations, and the scoped `CompositeDomainEventDispatcher`
invokes applicable handlers without a runtime registry or mediator adapter. The dispatch APIs retain
the `PandaBear.Domain` namespace and `PandaBear.SharedKernel.Domain` package boundary.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain`. C# namespace: `PandaBear.Domain`. Assembly/project identity: `SharedKernel.Domain`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
