# PandaBear.SharedKernel.EntityFrameworkCore.Sqlite

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

SQLite support for the shared-kernel Entity Framework Core primitives: accent- and case-insensitive text search
(`SearchFold`, below) and the SQLite `IUniqueViolationParser`, which lets `DbContext.SaveChangesTranslatingUniqueViolations` report a unique-index
violation by application rule. The provider-neutral pieces (`HasUniqueRule`, `UniqueConstraintViolationException`) are
documented with [SharedKernel.EntityFrameworkCore](../SharedKernel.EntityFrameworkCore/README.md).

SQLite names the violated columns (`UNIQUE constraint failed: Customers.Email`), not a constraint, so the parser reports
the table and columns, and an index name for expression indexes. Primary-key violations are recognized the same way.

```csharp
services.AddSqliteUniqueViolationParser();   // registers IUniqueViolationParser
```

## Accent- and case-insensitive search

SQLite's `LIKE`, `lower()` and `NOCASE` fold only ASCII letters, so `ESLOVÊNIA` does not find `Eslovênia`. `SearchFold` compares
search keys instead: `SearchFold.Of(text)` is the key, computed in SQL by the deterministic scalar function `sharedkernel_search_fold`
(`SearchFold.FunctionName`), and `SearchFold.ContainsPattern(input)` turns the user's input into a `LIKE` pattern.

The key is `SearchKey.Of` of `PandaBear.SharedKernel.BuildingBlocks`, a host-agnostic fold you can also use in memory: lone
surrogates become U+FFFD, the text is decomposed (NFD), nonspacing, spacing combining and enclosing marks are dropped, the rest is
lowercased with the invariant culture, white-space runs become one space with none at either end, and the result is composed again
(NFC). Every letter and digit of every script stays: `Eslovênia` → `eslovenia`, `  São   Paulo` → `sao paulo`, `Москва` → `москва`,
`東京` and `ß` unchanged. It is not `Slug.CreateLenient`: a slug is an ASCII URL identifier that turns `東京`, `Москва` and `ß` into
separators, so those words could never be found, and its rules belong to URLs.

`ContainsPattern` returns `%key%` with the `%`, `_` and `\` the user typed escaped, so they match themselves; pass
`SearchFold.EscapeCharacter` (`\`) to `EF.Functions.Like`, which emits `LIKE … ESCAPE '\'`. Input without a letter or digit, blank
input included, has no pattern (`null`): it matches nothing, so decide before calling whether blank input means "everything" for
your page.

```csharp
services.AddDbContext<CatalogDbContext>(options => options.UseSqlite(connectionString).AddSearchFold());

protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasSearchFoldFunction();

if (SearchFold.ContainsPattern(query) is not { } pattern)
{
    return [];
}

var found = await db.Images
    .Where(image => EF.Functions.Like(SearchFold.Of(image.FileName), pattern, SearchFold.EscapeCharacter)
        || EF.Functions.Like(SearchFold.Of((string)(object)image.Alt), pattern, SearchFold.EscapeCharacter))
    .OrderBy(image => image.FileName).ThenBy(image => image.Id)
    .Take(20)
    .ToListAsync(cancellationToken);
```

- `AddSearchFold()` (on `DbContextOptionsBuilder` and `DbContextOptionsBuilder<TContext>`) adds an interceptor that registers the
  function on every connection the context opens. A connection that is already open when you pass it to `UseSqlite` is not opened
  by the context: call `SearchFold.Register(connection)` on it.
- `HasSearchFoldFunction()` maps `SearchFold.Of` to the function, so the comparison runs in SQL. A value-converted column goes in as
  its stored text with `(string)(object)column`, a cast Entity Framework Core drops from the SQL.
- A `NULL` column has the empty key and matches no pattern.

Limits:

- .NET has no full Unicode case folding, so lowercasing is per character: `ß` does not match `ss`, and full-width letters do not
  match their plain forms.
- The key depends on the Unicode data of the running .NET; rebuild any key you store or index when the runtime changes.
- The function runs once per row and no index can serve it, so every search scans the table. That is fine for thousands of rows.
  For large tables use an [FTS5](https://sqlite.org/fts5.html) table with the `trigram` tokenizer and `remove_diacritics 1`
  (SQLite 3.45 and later), which is indexed and finds case- and accent-insensitive substrings of three or more characters. The
  SQLite that `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15 brings (`SQLitePCLRaw.bundle_e_sqlite3` 2.1.13) is 3.53.3, and a test
  of this package checks that it supports that tokenizer.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore.Sqlite`. C# namespace: `PandaBear.EntityFrameworkCore.Sqlite`.
Assembly/project identity: `SharedKernel.EntityFrameworkCore.Sqlite`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
