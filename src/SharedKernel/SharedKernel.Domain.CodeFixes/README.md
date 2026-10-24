# SharedKernel.Domain.CodeFixes

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Code-fix project for SharedKernel domain-model encapsulation conventions.

## Purpose

This Roslyn component provides safe code fixes for the diagnostics in
`SharedKernel.Domain.Analyzers`. Reference it next to the analyzer package from domain-model
projects only.

## Current State

- `SKDOM001` offers "Make setter private". The fix replaces the setter's modifiers with `private`.
  It is offered only when no code outside the declaring type or its nested types assigns, compound
  assigns, increments, decrements, initializes (object initializer or `with`), deconstructs into, or
  null-conditionally assigns the property anywhere in the solution. It is not offered for interface,
  abstract, virtual, override, explicit interface, or interface-implementing setters.
- `SKDOM002` has no automatic fix: replacing an exposed collection type changes callers.

## Fix All

Fix All uses the batch fixer for `SKDOM001`. Each fix edits one setter and checks its own property,
so the edits do not depend on each other.

## Package boundary

This package owns fixes for `SharedKernel.Domain.Analyzers` diagnostics only. Do not add
repository-wide style fixes or test-only fixes here.

## See Also

- [SharedKernel.Domain.Analyzers](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Domain.Analyzers/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain.CodeFixes`. C# namespace: `PandaBear.Domain.CodeFixes`. Assembly/project identity: `SharedKernel.Domain.CodeFixes`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
