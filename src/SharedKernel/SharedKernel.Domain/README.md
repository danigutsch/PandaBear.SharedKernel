# SharedKernel.Domain

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Persistence-agnostic DDD primitives shared by bounded contexts.

The package also owns typed in-process domain-event dispatch contracts. Generated modules contribute
closed `IDomainEventDispatchHandler` implementations, and the scoped `CompositeDomainEventDispatcher`
invokes applicable handlers without a runtime registry or mediator adapter. The dispatch APIs retain
the `SharedKernel.Domain` namespace and `PandaBear.SharedKernel.Domain` package boundary.

## Value objects and identity

`ValueObject` is the base class for value objects compared by their equality components: derive from it and return the
components from `GetEqualityComponents`. Two instances are equal when they have the same runtime type and equal components in
the same order; `==` and `!=` treat two `null` values as equal. `IIdentified<TId>` exposes the stable identifier of any identified
model, and `IEntity<TId>` marks a domain entity with it. `[GenerateModelSupport(Identity = true)]` of
`PandaBear.SharedKernel.Domain.SourceGenerator` generates identity equality for a type that implements `IIdentified<TId>`.
The reusable value objects of `PandaBear.SharedKernel.BuildingBlocks` (money, contact data, dates, codes) derive from
`ValueObject`; this package has no package dependency.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain`. C# namespace: `SharedKernel.Domain`. Assembly/project identity: `SharedKernel.Domain`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
