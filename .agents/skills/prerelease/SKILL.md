---
name: prerelease
description: Use when merging several SharedKernel pull requests in a row, taking the candidate of a main run, publishing a GitHub prerelease, writing its release notes, moving a consuming repository's pin to a new candidate, recording Linux and Windows consumption evidence with merge-evidence, or pruning prereleases.
license: See repository LICENSE.txt
---

# Release a candidate and move consumers to it

The release rules are in [Release policy](../../../docs/release-policy.md). This skill gives the order of the steps and the
checks between them. Every step that publishes something (merging, creating a prerelease, pruning) needs the repository
owner's go-ahead for that step: approval of one prerelease does not cover the next.

## 1. Pull requests

- The PR title becomes the squash commit header, so it must be a Conventional Commit header of at most 100 characters.
  PR Metadata fails otherwise.
- Open the PR ready for review, not as a draft. Marking a draft ready starts CI again.
- The owner merges. When several PRs are waiting, merge one, then rebase each next one onto the new `main` and wait for
  its CI before merging it. Two PRs that are green on their own can still break `main` together: one renames a type
  the other uses. An admin merge skips the up-to-date rule, so nothing else catches this.

## 2. Take the candidate

1. Wait for the `SharedKernel validation` run of the push to `main` to succeed. It builds the candidate
   `<target>-alpha.<run>.<attempt>`. A push that only changes documentation skips packing and has no candidate.
2. Download it: `gh run download <run> -n candidate-packages -D <dir>`. The files are in `<dir>/<version>/`.
   The artifact is kept for 14 days.
3. Check `candidate.json`:
   - `libraryRevision` is the merge commit;
   - `libraryDirty` is `false`;
   - every `.nupkg` matches its SHA-256 in `packageSha256`. Symbol packages (`.snupkg`) have no hash.
   Stop on any mismatch.

## 3. Publish the prerelease

Publish only when a consumer needs the content. Write the notes first:

- start with `Changes since <previous prerelease>:`;
- one bullet per PR, with its number;
- **breaking** changes, behaviour changes and new dependencies (transitive ones included) in bold;
- end with `Prerelease only; nothing is published to a package registry.`

Then, from `<dir>/<version>`, with the files unchanged:

```bash
gh release create v<version> --prerelease --target <libraryRevision> --title "<version>" \
  --notes-file <notes.md> candidate.json *.nupkg *.snupkg
```

## 4. Move a consumer to it

In the consuming repository, on its own branch:

1. Copy `candidate.json` unedited over `.config/sharedkernel-candidate.json`.
2. Move the version in `Directory.Packages.props` and `.config/dotnet-tools.json`.
3. `global.json` must have the same `sdk.version` and `sdk.rollForward` as this repository at `libraryRevision`, and
   the library checkout that `stage-candidate` uses must be clean at that revision; it checks both.
4. Stage the packages with `dotnet ci stage-candidate` through the consumer's bootstrap. It checks the pin, the SDK, the
   revision and every SHA-256 again.
5. Build and run every test suite. Adapt to the breaking changes in the notes.
6. Commit the pin as `chore(sharedkernel): adopt candidate <version>`. Commit each code change it needs separately.

## 5. Record consumption evidence

1. In this repository, `gh workflow run ci.yml -f candidate_version=<version>` stages the published prerelease.
   It consumes it on Linux and Windows, including NativeAOT.
2. When the run succeeds, download its `published-candidate-*` artifacts.
3. In the consumer, run
   `dotnet release merge-evidence --candidate .config/sharedkernel-candidate.json --evidence <each consumption.json>`.
   Never edit the pin by hand.
4. Commit as `chore(sharedkernel): record Linux and Windows consumption of <version>`.

## 6. Prune old prereleases

Run the `Prune prereleases` workflow:

- first with `apply=false`, to preview;
- then with `apply=true`.

Set `keep`, and list in `protect` every version a consumer still pins. Never protect the newest prerelease: protected
versions do not count toward `keep`, so protecting it keeps an older one instead. Tags always stay, and a version is never
reused.
