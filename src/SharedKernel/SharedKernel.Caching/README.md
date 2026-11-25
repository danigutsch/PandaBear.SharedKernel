# PandaBear.SharedKernel.Caching

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Dependency-free in-process caching helpers.

## Usage

`RevisionCache<TKey, TValue>` caches read models in memory under a shared revision number: a value that grows by one in the same
transaction as every change to the cached data, for example a single database row. Every process that reads the data keys its cache by that
number, so an edit saved by another host shows up without a message bus or cache invalidation call.

- The revision is read at most once per `revisionCheckInterval`. That interval is the longest an edit can take to show; zero reads it on every
  call.
- While the revision is unchanged, `Get(key, ct)` returns the cached value. When a newer revision is seen, every cached value is dropped at
  once and the next `Get` of each key loads it again at the new revision. Memory holds at most one value per key.
- A revision lower than one already seen, for example from a lagging replica, is ignored. A failed load is not cached. A load that finishes
  after a newer revision was seen returns its value to its caller but is not stored. Two concurrent misses of one key may both load; the
  first stored value is kept.

Register one instance per value kind as a singleton and keep the values immutable: every caller gets the same instance. Who increments the
revision, and in which transaction, stays with the application; with Entity Framework Core, `SaveChangesIncrementingRevision` of
`SharedKernel.EntityFrameworkCore` increments a single revision row in the same transaction as every content change.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Caching --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using SharedKernel.Caching;

public sealed record Catalog(long Revision, IReadOnlyList<string> Titles);

public sealed class CatalogReader(ICatalogStore store, TimeProvider time)
{
    private readonly RevisionCache<string, Catalog> _cache = new(
        store.ReadRevision,
        async (culture, revision, ct) => new Catalog(revision, await store.ReadTitles(culture, ct)),
        TimeSpan.FromSeconds(5),
        time);

    public ValueTask<Catalog> Get(string culture, CancellationToken ct) => _cache.Get(culture, ct);
}

public interface ICatalogStore
{
    ValueTask<long> ReadRevision(CancellationToken ct);

    Task<IReadOnlyList<string>> ReadTitles(string culture, CancellationToken ct);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Caching`. C# namespace: `SharedKernel.Caching`. Assembly/project identity: `SharedKernel.Caching`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
