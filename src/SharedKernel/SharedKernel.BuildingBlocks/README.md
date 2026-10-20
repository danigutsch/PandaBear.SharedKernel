# SharedKernel.BuildingBlocks

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable value-object and workflow building blocks shared by bounded contexts.

## Identity and optional lookups

`IIdentified<TId>` exposes a stable identifier for any identified model. It is owned by this package;
domain entities compose it through `SharedKernel.Domain.IEntity<TId>`.

`Option<TEntity>.ToNotFoundResult(id, container)` maps an empty optional identified model to a
`SharedKernel.Results.Result<TEntity>` with a standard not-found detail. `container` is optional and
preserves aggregate context such as `"this tour"`. This package references `SharedKernel.Results` for
the result type.

## Compensation

`Compensation.CompleteOrCompensate` runs a follow-up operation and invokes a caller-provided
compensation action if that operation fails. Use it for small best-effort cleanup around side
effects that cannot share a transaction.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.BuildingBlocks`. C# namespace: `PandaBear.BuildingBlocks`. Assembly/project identity: `SharedKernel.BuildingBlocks`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
