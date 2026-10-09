# Private SharedKernel development and extraction

SharedKernel remains in ViajantesTurismo until a separate consumer migration is reviewed.
Initial distribution uses cloned repositories and local NuGet feeds. Package IDs, public APIs,
target frameworks, lock files, and the Microsoft.Testing.Platform runner remain unchanged.
No script publishes packages, creates a remote repository, replaces dependencies, or cuts over
application references.

## Extract a disposable local repository

Commit changes to retained source paths first, then run from an application development worktree:

```sh
python3 scripts/sharedkernel/test-workflow.py
python3 scripts/sharedkernel/extract.py .local/sharedkernel
```

Install and review `git-filter-repo` before running extraction. The verified extraction used
version 2.47.0 in a temporary Python virtual environment. This is an external development tool
and a supply-chain dependency; it is not an application or library package dependency.
The script does not install tools itself.

The explicit `scripts/sharedkernel/extraction-manifest.json` inventories projects, retained paths,
deferred projects, and package-consumption edges. Extraction validates project-reference closure,
clones with independent Git objects, filters only the new clone, and refuses existing destinations.
It retains the commit map, original and filtered revisions, and manifest digest under `provenance/`.
Historical renames are not followed automatically by git-filter-repo: review and add old paths to
this manifest when extracting an earlier layout.

Adaptations occur only in the destination's repository-local development worktree. They create
`SharedKernel.slnx`, update test repository discovery, preserve inherited analyzer and editor
configuration, change the SBOM document title to SharedKernel, and remove application friend
assemblies. The original repository keeps its application friends and root discovery behavior.
Repository URLs remain the original source URLs until a destination remote is chosen; this preserves
source provenance during private development. Retain the existing symbol packages for local
debugging. Configure and verify Source Link against the destination repository after choosing its
remote: the filtered commit IDs do not resolve in the original repository. Do not commit source
project references between repositories.

The mixed RepoConfig tool and tests stay with the application, as do the documentation renderer,
documentation tool, and tests that include application route-reporting behavior. Reusable selection
and extraction scripts are copied separately; application roadmap, OpenAPI orchestration, and
architecture checks are not copied. Split the deferred modules before any subsequent extraction.
Review and commit destination adaptations before producing compatibility evidence.

## Validate candidate packages

From the standalone development worktree:

```sh
python3 scripts/sharedkernel/select-projects.py
python3 scripts/sharedkernel/validate.py --pack
```

For an affected run, supply `--base <revision>` to selection. Missing history, unknown owners,
unevaluated imports or references, and global build inputs select full validation. Git comparison
uses merge-base and reports both old and new paths for renames. Selection includes owning projects,
reverse transitive dependents, explicit package-consumption edges, and complete test projects.
The output includes build and test project lists and selection reasons. Test-method filters remain
an independent local debugging tool.

Candidate validation uses locked restore, Release builds, complete selected test projects, public
API baselines, strict API command failure handling, package restore with an isolated cache, and
existing sample programs restored, built, and run through exact centrally managed package versions.
Optional `--baseline-version <version>` enables comparison with a previously accepted baseline.
It requires `NuGet.Config` to map `SharedKernel.*` exclusively to an existing local baseline feed;
the runner refuses a baseline comparison that could resolve internal IDs from the public feed.
Initial baselines and subsequent artifacts must be available through the private local-feed workflow.
Baseline absence does not constitute proof of compatibility with a previous release.

Packages use a unique prerelease version. Existing versions are never overwritten. Internal package
IDs map exclusively to the local feed, including internal packages absent from that feed; missing
packages cannot resolve from a public package with the same ID. Smoke consumers use `[version]`
exact ranges and fresh caches. Their source programs are copied into ignored scratch projects;
no cross-repository `ProjectReference` is committed.

The generated standalone CI workflow builds and tests selected projects on library PRs and performs
full validation nightly and on main. It checks packed samples on Linux and consumes the same hashed
artifacts on Windows, including a representative mediator Native AOT publish and executable run.
Full-suite coverage trends belong to full validation runs; selective runs produce project evidence.
Existing application Sonar coverage policy is outside the standalone extraction workflow.

Provider tests require their existing container dependencies. Native AOT requires the platform's
native toolchain. An unavailable environment remains pending evidence, not a successful gate.

## Separate application compatibility and promotion

After library merge, use the retained candidate artifacts in a separately prepared application
worktree. This initial extraction does not prepare the application dependency cutover.

```sh
python3 scripts/sharedkernel/compatibility.py <consumer-worktree> <candidate.json>
```

The compatibility command requires committed repository revisions, unchanged package hashes,
centrally pinned exact candidate versions, and removal of application source references to
SharedKernel. It uses an isolated package cache and locked restore, then builds and runs complete
application tests. A consumer that still uses source references produces pending evidence and
returns a failing exit code. Compatibility is a separate adoption gate, not a library merge gate.

Before stable promotion, require full library validation, provider tests, Linux and Windows package
consumption, analyzer and generator activation, API baseline comparison, promised AOT checks, and
passing application compatibility. Record both repository revisions and the package hashes.
Promote these same artifacts; do not rebuild or silently replace a tested package version.
Retain the application's prior dependency configuration for rollback in the separate cutover change.

Remote feeds and public release require a later decision. Existing `SharedKernel.*` package naming
collides with packages owned by [algorama](https://www.nuget.org/profiles/algorama).
Before public release, compare [Mediator](https://github.com/martinothamar/Mediator),
[Ardalis.Result](https://github.com/ardalis/Result), and
[Ardalis.SharedKernel](https://www.nuget.org/packages/Ardalis.SharedKernel/) against the supported
capabilities. A rename alone does not address overlapping functionality. Migration does not replace
these dependencies or choose a public package name.

References: [local NuGet feeds](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds),
[package source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping),
[git-filter-repo](https://github.com/newren/git-filter-repo), and
[.NET package validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/package-validation/overview).

## Library version policy

Use SemVer 2.0 for the coordinated SharedKernel package set: patch for compatible fixes,
minor for compatible additions, and major for breaking changes. Keep initial extraction under
`0.1.0-alpha.*`; do not imply stable compatibility before a reviewed 1.0 release. Conventional
Commit analysis in `SharedKernel.Versioning` calculates release impact, but API validation and
human review must also account for behavioral changes and supported target frameworks.

Use unique prerelease identifiers for every development pack. Build metadata (`+sha.*`) does
not create a distinct NuGet package identity, so it cannot prevent overwriting versions. Record
the source revision and package SHA-256 alongside each candidate. Keep stable assembly identity
within a major release (`Major.0.0.0`), file versions numeric (`Major.Minor.Patch.0`), and the full
SemVer plus source revision in informational versions. The existing `VersionOutput` implementation
already applies these rules; do not change assembly identity merely for a prerelease suffix.

Consumed dependency versions are minimum compatible versions (`10.0.11` means
`[10.0.11,)`), allowing newer dependencies without exact pins or speculative upper bounds.
Application lock files preserve reviewed resolutions in CI; explicit upgrades refresh those
locks and run compatibility checks. Published library dependency
ranges retain their declared compatibility requirements; application pins do not redefine the
library API contract. Compare release candidates against the last accepted baseline and preserve
immutable tested artifacts. A stable version has a different package identity from a prerelease:
if that identity is needed, create and validate the stable artifact once before promoting it.
Do not relabel a prerelease archive or repack an already tested version.

See [Microsoft library versioning guidance](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning)
and [NuGet version semantics](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning).


## Transitional consumer tests

ViajantesTurismo still has tests of internal persistence, relay, and ASP.NET Core testing types.
Their friend declarations remain temporarily so package adoption preserves those risk guards.
The package consumer build reproduced inaccessible-member errors when those declarations were
removed. They do not add public API. Remove these declarations only after transferring reusable
internal tests and adapting application composition checks to public contracts. Until then, this
application coupling is recorded as pending extraction cleanup and blocks stable promotion.
