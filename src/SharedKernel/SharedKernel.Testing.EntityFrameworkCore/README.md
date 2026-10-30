# SharedKernel.Testing.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

A minimal Entity Framework Core context and entity for SharedKernel test suites.

## Usage

```csharp
using PandaBear.Testing.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<TestDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
    .Options;
await using var context = new TestDbContext(options);
context.Entities.Add(new TestEntity { Name = "first" });
await context.SaveChangesAsync();
```

`TestDbContext` exposes `Entities`, a `DbSet<TestEntity>`. A test that only needs a context as a transaction target can ignore the set.
