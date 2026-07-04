# SharedKernel.Versioning.Tool

Local and CI versioning automation for SharedKernel release flows.

## Commands

Calculate release impact for one Conventional Commit message:

```text
sharedkernel-version commit-impact "feat(versioning): emit JSON output"
```

Calculate version output from git history:

```text
sharedkernel-version compute --base 0.1.0 --since v0.1.0 --prerelease alpha.1 --sha abc123
```

Output fields:

- `semVer`
- `releaseImpact`
- `packageVersion`
- `assemblyVersion`
- `fileVersion`
- `informationalVersion`

The `compute` command reads commit messages from `git log`. Non-Conventional merge commits are
ignored; valid Conventional Commit messages drive the release impact.
