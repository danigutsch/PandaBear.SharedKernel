# SharedKernel.Functional

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Transition package during the `SharedKernel.Results` extraction.

## Purpose

`SharedKernel.Functional` previously owned the repository's `Result` and `Option` primitives.

Those primitives now live in `SharedKernel.Results` as part of the shared results extraction.

## Current state

This project remains in the solution only as a temporary migration placeholder while project names,
sample names, benchmark names, and any remaining documentation references are cleaned up in
follow-up work.

Do not add new result-oriented primitives here.

Use `SharedKernel.Results` instead.

## See also

- [SharedKernel.Results](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Results/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Functional`. C# namespace: `PandaBear.Functional`. Assembly/project identity: `SharedKernel.Functional`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
