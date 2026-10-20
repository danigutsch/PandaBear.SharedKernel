# Development and quality

Install Git and the exact .NET SDK in `global.json`; SDK roll-forward is disabled intentionally. From a fresh clone, run `bash setup-dev.sh` on Linux or `pwsh -File setup-dev.ps1` on Windows. These scripts restore locked dependencies and build Release without accessing an application checkout. No global Git configuration, Node installation, or sibling repository is required.

Run the complete tests with `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Integration tests require Docker or a compatible working Podman runtime. Keep parallel test modules enabled; inspect container diagnostics rather than masking startup or cleanup errors. NativeAOT consumption needs the platform native compiler/linker: install the SDK's supported C++ build prerequisites on Linux and Visual Studio C++ build tools on Windows.

Run `dotnet format SharedKernel.slnx --verify-no-changes --no-restore`, then `dotnet run --project tools/SharedKernel.Documentation.Tool -c Release -- generate --config docs/architecture/generated-diagrams.json --check`. Hosted quality checks validate formatting and generated diagram drift on clean checkouts. Repository validation selects complete test projects; nightly runs cover all projects. Package CI builds immutable artifacts on Linux and consumes those same hashed packages on Windows, including NativeAOT.

Use file-scoped namespaces, nullable references, four spaces, one top-level type per new file, and XML documentation on public APIs. Do not append `Async` to methods. Preserve warnings-as-errors, vulnerability auditing, public API validation, authorization, validation, privacy, and meaningful regressions. Prove actionable behavior gaps with failing tests before production changes. Prefer existing .NET tools and platform APIs; review any new dependency, including transitives.

## Windows ESLint report

The reported missing-configuration error has not been reproduced: the exact Windows command, working directory, ESLint version, and full error were not supplied. Inspection found no tracked ESLint configuration, Node package manifest/lock, ESLint task, editor recommendation, or CI invocation in either repository. ViajantesTurismo tracks two authored k6 JavaScript scenarios and ten vendored Bootstrap JavaScript files; its `docs/CODE_QUALITY.md` mentions ESLint although `scripts/lint-all.sh` does not invoke it. That claim does not establish an installed lint workflow. This standalone repository contains no tracked JavaScript/TypeScript sources.

Linux has no `eslint` executable in the inspected environment. An editor extension, global command, or external configuration remains a hypothesis, not a demonstrated cause. Capture the exact error, command/editor Output channel, version, and working directory before changing the application lint scope or choosing a version-specific configuration. No empty ESLint configuration or blanket ignore is added here.

The user's follow-up identifies a Git-hook suspicion. The application has an untracked archived `.git/hooks/pre-commit.backup-opencode` at lines 103–113 that explicitly invokes `eslint` against JSON with `--fix`; the current `.git/hooks/pre-commit` invokes `scripts/lint-all.sh`, which has no ESLint step. On inspected Linux, effective global `core.hooksPath` points to `~/.config/git/hooks-disabled`, an empty directory. Inspected Windows global Git configuration has no `core.hooksPath`; repository access through the UNC path fails Git's dubious-ownership guard, so the effective Windows repository hook was not established. This proves a stale archived invocation exists, not that it produced the original error. Do not change global Git configuration or add an empty ESLint configuration. Capture the active Windows hook path and exact error once normal trusted repository access is established.

## NativeAOT prerequisites

Install the repository-pinned .NET SDK first. On Ubuntu, install the native compiler and compression development library with `sudo apt-get install clang zlib1g-dev`. On Windows, install Visual Studio 2022 or later with the **Desktop development with C++** workload and its default components. These are platform build prerequisites, not new repository package dependencies. Build NativeAOT on the operating system being validated; a Linux run does not prove Windows consumption.

These commands and workload requirements follow the [official NativeAOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#prerequisites), checked 2026-10-20. Other distributions require their own documented compiler/development packages.

## Status evidence

Build badges await a verified private remote and real workflow URL. Coverage is collected locally with `coverlet.MTP`; no hosted coverage service or percentage is configured. Registry publication is deferred, so no version/publication badge may claim a public package exists. Add badges only when intended readers can access their actual evidence without credentials in URLs.
