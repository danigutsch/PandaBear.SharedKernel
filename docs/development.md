# Development and quality

Install Git and the exact .NET SDK in `global.json`; SDK roll-forward is disabled intentionally. From a fresh clone, run `bash setup-dev.sh` on Linux or `pwsh -File setup-dev.ps1` on Windows. These scripts restore locked dependencies and build Release without accessing an application checkout. No global Git configuration, Node installation, or sibling repository is required.

Run the complete tests with `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Integration tests require Docker or a compatible working Podman runtime. Keep parallel test modules enabled; inspect container diagnostics rather than masking startup or cleanup errors. NativeAOT consumption needs the platform native compiler/linker: install the SDK's supported C++ build prerequisites on Linux and Visual Studio C++ build tools on Windows.

Run `dotnet format SharedKernel.slnx --verify-no-changes --no-restore`, then `dotnet run --project tools/SharedKernel.Documentation.Tool -c Release -- generate --config docs/architecture/generated-diagrams.json --check`. Hosted quality checks validate formatting and generated diagram drift on clean checkouts. Repository validation selects complete test projects; nightly runs cover all projects. Package CI builds immutable artifacts on Linux and consumes those same hashed packages on Windows, including NativeAOT.

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
