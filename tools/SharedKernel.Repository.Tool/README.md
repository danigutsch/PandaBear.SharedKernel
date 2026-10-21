# Repository validation tool

This non-packaged C# executable selects live solution projects and runs locked restores, Release builds, and complete MTP test projects. Use `select
--base <revision>` followed by `validate`; omit the base for full validation. `validate --pack --aot` delegates immutable candidate creation to
SharedKernel.Versioning.Tool. Selection files and reasons are written to `TestResults/selection`. Explicit package-consumption dependencies are
maintained in `package-consumption-edges.json`.

`select --github-output <path>` appends `candidate_required=true` or `candidate_required=false` to the supplied GitHub Actions output file, preserving existing output. Relative output paths resolve from the selected repository root.


CI restores and builds selected projects as one temporary solution graph. `validate --test-group
ordinary` runs complete non-hosted MTP projects with native module scheduling. Passing a selected
hosted project path as `--test-group` builds and runs only that hosted project. The default `all`
keeps hosted execution separate from ordinary modules. No module-parallelism cap is introduced.

Selection records its checkout revision in `revision.txt`; validation rejects stale plans and
projects outside the live solution. Execution timings and failures are retained under
`TestResults/selection/execution-*`. `select --github-output` also emits the selected hosted matrix,
so new Aspire test projects do not require a manually synchronized workflow list.

`verify-pr-title --event <GitHub event JSON>` validates the squash commit header. The metadata
workflow runs independently so PR edits cannot replace source-validation outcomes.
