# Development and quality

Install Git and the exact .NET SDK in `global.json`; SDK roll-forward is disabled intentionally. From a fresh clone, run `bash setup-dev.sh` on Linux or `pwsh -File setup-dev.ps1` on Windows. These scripts restore locked dependencies and build Release without accessing an application checkout. No global Git configuration, Node installation, or sibling repository is required.

Run the complete tests with `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Integration tests require Docker or a compatible working Podman runtime. Keep parallel test modules enabled; inspect container diagnostics rather than masking startup or cleanup errors. NativeAOT consumption needs the platform native compiler/linker: install the SDK's supported C++ build prerequisites on Linux and Visual Studio C++ build tools on Windows.

Run `dotnet format SharedKernel.slnx --verify-no-changes --no-restore`, then `dotnet run --project tools/SharedKernel.Documentation.Tool -c Release -- verify --config docs/architecture/generated-diagrams.json`. Hosted quality checks validate formatting and generated diagram drift on clean checkouts. Repository validation selects complete test projects; nightly runs cover all projects. Package CI builds immutable artifacts on Linux and consumes those same hashed packages on Windows, including NativeAOT.

Use file-scoped namespaces, nullable references, four spaces, one top-level type per new file, and XML documentation on public APIs. Do not append `Async` to methods. Preserve warnings-as-errors, vulnerability auditing, public API validation, authorization, validation, privacy, and meaningful regressions. Prove actionable behavior gaps with failing tests before production changes. Prefer existing .NET tools and platform APIs; review any new dependency, including transitives.

## JavaScript tooling

This repository contains no tracked JavaScript/TypeScript sources and declares no ESLint or Node toolchain. Setup and CI use the pinned .NET SDK and explicit platform prerequisites. An inherited editor or Git-hook command requiring ESLint is not a library requirement; identify the effective hook/configuration and its exact error before changing lint scope. Do not add empty lint configuration or blanket ignores to hide an external invocation.

## NativeAOT prerequisites

Install the repository-pinned .NET SDK first. On Ubuntu, install the native compiler and compression development library with `sudo apt-get install clang zlib1g-dev`. On Windows, install Visual Studio 2022 or later with the **Desktop development with C++** workload and its default components. These are platform build prerequisites, not new repository package dependencies. Build NativeAOT on the operating system being validated; a Linux run does not prove Windows consumption.

These commands and workload requirements follow the [official NativeAOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#prerequisites), checked 2026-10-20. Other distributions require their own documented compiler/development packages.

## Status evidence

The repository-wide build badge links to the [main validation workflow](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml); it reports repository validation rather than per-package coverage or publication. Coverage is collected locally with `coverlet.MTP`; no hosted coverage service or percentage is configured. Registry publication is deferred, so no version/publication badge may claim a public package exists. Add badges only when intended readers can access their actual evidence without credentials in URLs.

## Engineering review guidance

Document stable caller-visible guards, cancellation, and deliberate adapter failures using the [exception documentation policy](EXCEPTION_DOCUMENTATION.md). Review diagnostic enforcement and scoped exceptions using the [warning policy](ci/warning-policy.md). Review upstream Action SHAs and hosting/tool image digests using the [pin-update workflow](ci/pin-update-review.md).


## NativeAOT and reflection policy

Prefer NativeAOT and trimming compatibility where practical. Use explicit typed APIs, existing source generators, and supplied JSON metadata instead of runtime type discovery or reflection-based serialization when they meet the current use case. Do not add speculative generators or abstractions merely to avoid a small, justified use of reflection.

`src/Directory.Build.targets` enables `IsAotCompatible` by default for compatible target frameworks. Keep its analyzers enabled; an explicit opt-out must explain the actual dependency or host limitation beside the project setting. When reflection is necessary, preserve the required trim annotations and document the supported boundary. SDK compatibility analysis is not execution proof: validate promised NativeAOT behavior through packed consumers on each claimed platform and keep untested packages or scenarios explicit.

## Required automation coverage

Reusable repository automation lives in the C# CI, versioning, and documentation tools.
The two setup scripts are deliberately thin platform entry points: they anchor the working directory,
restore locked dependencies, build Release, and propagate failures before repository tools exist.
C# process tests exercise each native wrapper from an unrelated working directory, including paths
with spaces, successful execution, restore failure, and build failure. CI runs the contract tests on
Linux and Windows.

Workflow contract tests guard the active C# commands and their platform test wiring. Selection output
and candidate manifest discovery belong to the C# commands, with regression tests for failure paths.
A test pass does not imply line coverage of shell syntax; actual clean-checkout bootstrap and packed
candidate jobs provide integration evidence.


## CI throughput and squash merging

PR validation is advisory because main has no branch protection. Use squash merging with a
Conventional Commit PR title of at most 100 characters, preserve breaking-change notes in the PR
body, and start a fresh branch after merging. Registry publication is deferred.

The PR metadata workflow checks the title and breaking-change footers with the CI tool's
`verify-commit-message --event`. Its `--file` option checks a local commit message against the
same header and footer rules, as the
[CI tool README](../tools/SharedKernel.CI.Tool/README.md#verify-commit-message) describes.

Ordinary PR title/body edits run only the independent metadata workflow. Add `ci:rerun` to explicitly
restart source validation; remove and reapply for another attempt using a human or authorized app
account. Unrelated labels use separate concurrency groups and cannot cancel validation.

The source workflow selects test projects once with `sharedkernel-ci select`, as `.config/sharedkernel-ci.json`
configures, then overlaps one `validate` job per test slice with selected test projects, which runs `sharedkernel-ci run
--slice <name>`, sparse-checkout source builds of the CI and RepoConfig tools on Linux and Windows, and candidate
creation. A change to a project selects the test projects that depend on it; a change to a repository-wide build input
(`.config`, `.github`, `*.props`, `*.targets`, `*.slnx`, analyzer configuration, `global.json`, `NuGet.Config`, the
style analyzer tests, or the shared test configuration) selects every test project. Any project change runs the
candidate job, which packs and consumes every package, and repository quality, which builds the whole solution and runs
the governed literal check (`--explicit only`) against every library. Each Aspire AppHost test project has a slice of
its own; the `library` slice runs every other selected test project. Linux and Windows consume the same candidate bytes
in parallel. Repository quality runs on every validation. Standalone repository documentation skips candidates, tests,
and full-solution bootstrap/formatting while every tracked build file is inert; repository quality then builds only the
documentation tool and checks generated-document drift. Package READMEs, scripts, and uncertain inputs are not
documentation-only and select validation. Main pushes compare their before/after revisions; nightly runs and manual
runs without `candidate_version` select full validation. Invalid comparison history falls back to full validation.

The repository allows only squash merging, uses the PR title and body as the default squash commit
message, deletes merged branches automatically, and defines the `ci:rerun` label.

Elapsed workflow times include queueing and postprocessing. Before claiming a CI speedup, compare
several runs of the same change class, such as documentation-only changes, and their per-phase
timings; one run is not evidence of a general speedup. CI uses standard GitHub-hosted runners
without a merge queue, and the repository belongs to a personal account.
