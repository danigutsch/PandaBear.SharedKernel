# SharedKernel.Functional

Transition package during the `SharedKernel.Results` extraction.

## Purpose

`SharedKernel.Functional` previously owned the repository's `Result` and `Option` primitives.

Those primitives now live in `SharedKernel.Results` as part of issues `#76` and `#79` under epic
`#74`.

## Current state

This project remains in the solution only as a temporary migration placeholder while project names,
sample names, benchmark names, and any remaining documentation references are cleaned up in
follow-up work.

Do not add new result-oriented primitives here.

Use `SharedKernel.Results` instead.

## See also

- [SharedKernel.Results](../SharedKernel.Results/README.md)
