# SharedKernel.Domain.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Analyzer project for SharedKernel domain-model encapsulation conventions.

## Purpose

This Roslyn component reports domain state that callers can change without going through behavior.
Rich domain models keep invariants inside entities and aggregates: state changes through
intent-revealing methods, and collections are exposed as read-only views.

Reference this package only from domain-model projects. The analyzer treats every property in the
compilation as domain state, so the package reference is the activation boundary. Do not reference
it from DTO, read-model, persistence, API, or test projects.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKDOM001` | Warning | Properties should not expose a non-private, non-`init` setter. |
| `SKDOM002` | Warning | Properties visible outside the assembly should not expose mutable collection types. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKDOM001.severity = warning
dotnet_diagnostic.SKDOM002.severity = warning
```

## SKDOM001

Reports a property whose setter is `public`, `internal`, `protected`, `protected internal`, or
`private protected`, including setters required by a domain interface or an explicit interface
implementation. The diagnostic points at the `set` accessor.

```csharp
public sealed class Tour
{
    public string Name { get; set; } // SKDOM001
}
```

Not reported: `private set`, `init`, `private init`, get-only and expression-bodied properties,
positional record properties, overrides (the base declaration reports), and generated code.

Fix: change state through a method that validates the change, and make the setter private.
`SharedKernel.Domain.CodeFixes` offers "Make setter private" when nothing outside the declaring type
writes the property.

## SKDOM002

Reports a property visible outside the assembly whose type is, or implements, `ICollection<T>` or
`ICollection`, such as `IList<T>`, `ICollection<T>`, `ISet<T>`, `HashSet<T>`, `Collection<T>`, or
`Dictionary<TKey, TValue>`.

```csharp
public sealed class Tour
{
    public IList<string> IncludedServices { get; } = new List<string>(); // SKDOM002
}
```

Not reported: `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `IReadOnlyDictionary<TKey, TValue>`,
`IEnumerable<T>`, `ReadOnlyCollection<T>` and types derived from it, `ReadOnlyDictionary<TKey, TValue>`,
`ReadOnlySet<T>`, `System.Collections.Immutable` and `System.Collections.Frozen` types, internal or
private members, overrides, type parameters, and generated code. Arrays and `List<T>` are left to
CA1819 and CA1002, and visible fields to CA1051, so enable those SDK rules alongside this package.

Fix: keep a private collection, expose `IReadOnlyCollection<T>` or `IReadOnlyList<T>` (for example
`items.AsReadOnly()`), and add intent-revealing methods for each change. There is no automatic fix
because changing the exposed type changes callers.

## Package boundary

This package owns domain-model encapsulation diagnostics. Keep repository-wide style rules in
`SharedKernel.Style.Analyzers`, test-only rules in `SharedKernel.Testing.Analyzers`, and model
support generation in `SharedKernel.Domain.SourceGenerator`.

## See Also

- [SharedKernel.Domain.CodeFixes](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Domain.CodeFixes/README.md)
- [SharedKernel.Domain](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Domain/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain.Analyzers`. C# namespace: `PandaBear.Domain.Analyzers`. Assembly/project identity: `SharedKernel.Domain.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
