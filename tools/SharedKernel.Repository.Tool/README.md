# Repository validation tool

This non-packaged C# executable selects live solution projects and runs locked restores, Release builds, and complete MTP test projects. Use `select
--base <revision>` followed by `validate`; omit the base for full validation. `validate --pack --aot` delegates immutable candidate creation to
SharedKernel.Versioning.Tool. Selection files and reasons are written to `TestResults/selection`. Explicit package-consumption dependencies are
maintained in `package-consumption-edges.json`.

`select --github-output <path>` appends `candidate_required=true` or `candidate_required=false` to the supplied GitHub Actions output file, preserving existing output. Relative output paths resolve from the selected repository root.
