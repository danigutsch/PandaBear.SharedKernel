# SharedKernel.Testing.CodeFixRunner

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Local code-fix runner for SharedKernel testing analyzer migrations.

This package is a .NET tool. The command name is `sharedkernel-codefix`.

## Synopsis

```text
sharedkernel-codefix --help
sharedkernel-codefix --version
sharedkernel-codefix [--diagnostic <id>] <project-or-solution>
```

## Restore or install

Install from a local package during development:

```text
dotnet pack tools/SharedKernel.Testing.CodeFixRunner/SharedKernel.Testing.CodeFixRunner.csproj -o artifacts/local-tool-feed
dotnet tool install PandaBear.SharedKernel.Testing.CodeFixRunner --tool-path ./.tools --version <validated-candidate-version> --add-source artifacts/local-tool-feed
./.tools/sharedkernel-codefix --help
```

When installing from this repository, follow the local-feed source-mapping notes in
[release policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/release-policy.md).

Install as a global tool only from a trusted package source:

```text
dotnet tool install --global PandaBear.SharedKernel.Testing.CodeFixRunner --version <validated-candidate-version> --add-source <trusted-package-source>
sharedkernel-codefix --help
```

`.NET` tools run in full trust. Restore or install only from trusted manifests and package sources.

## Commands

Run the default testing analyzer migration against a project or solution:

```text
sharedkernel-codefix tests/SharedKernel.Versioning.Tests/SharedKernel.Versioning.Tests.csproj
```

The default diagnostic is `SKTEST004`. The runner applies the safe code fix only for static helper
methods that can be moved to a dedicated helper file without test-instance state. It skips local
functions, nested helper types, overloaded helpers, and instance-dependent helpers for manual review.

Run a specific diagnostic migration:

```text
sharedkernel-codefix --diagnostic SKTEST006 SharedKernel.slnx
```

## Exit codes

- `0`: command succeeded.
- `2`: invalid command, invalid arguments, or invalid input.

Errors are written to standard error. Command output is written to standard output.

## Packaging notes

- `PackAsTool=true` packages the console application as a .NET tool.
- `ToolCommandName=sharedkernel-codefix` controls the command used after install.
- `PackageReadmeFile=README.md` includes this usage guide in the NuGet package.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.CodeFixRunner`. C# namespace: `PandaBear.Testing.CodeFixRunner`. Assembly/project identity: `SharedKernel.Testing.CodeFixRunner`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
