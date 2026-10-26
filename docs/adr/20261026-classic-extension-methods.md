# Classic extension methods

Date: 2026-10-26. Status: accepted.

SharedKernel declares extension members as classic static extension methods. No C# `extension(...)` block exists in the repository, and
none is introduced while the compiler-emitted nested type trips CA1034 (nested types should not be visible), which `.editorconfig` keeps at
`warning` under warnings-as-errors. Suppressing CA1034 for the generated shape would need a repository-wide exception that this repository
does not grant for a stylistic change.

Revisit the decision when the pinned SDK stops reporting CA1034 for extension blocks, or when a member cannot be expressed as a classic
extension method, such as an extension property. A conversion then lists the converted classes, updates the public API baselines
intentionally, and keeps the analyzer configuration as strict as before for every other nested type.
