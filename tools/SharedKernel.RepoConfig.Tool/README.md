# SharedKernel repository configuration tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`dotnet repo` maintains repository roadmap configuration, its optional GitHub projection, dependency
snapshots, and repository checks. It is a command-line wrapper over generic engines: the forge-neutral
`PandaBear.SharedKernel.Repositories` library, its GitHub provider `PandaBear.SharedKernel.Repositories.GitHub`, its
Forgejo provider `PandaBear.SharedKernel.Repositories.Forgejo` for label sync, and `PandaBear.SharedKernel.DevContainers`
for the dev container and Podman checks. The consuming repository owns its policy and data, such as the license
exceptions and documentation link scope in `.config/sharedkernel-repo.json`, its workflows, and protected-environment
approvals.

## Prerequisites

The tool targets the repository's .NET version (`RepositoryTargetFramework`) and rolls forward to a later major runtime when that one is not installed.
`verify-text-encoding`, `verify-line-endings`, `verify-json`, `verify-gherkin`, `verify-target-frameworks`,
`dependency snapshot`, `dependency verify-license-exceptions`, and the `automation` steps also need Git on an absolute
`PATH` entry.
`verify-devcontainer` needs Docker on an absolute `PATH` entry, a container image with `bash`, and, without
`--devcontainer-cli`, a Linux x64 host with HTTPS access to `nodejs.org` and `registry.npmjs.org`.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`dotnet ci stage-candidate` stages a candidate in. Add the feed to `NuGet.Config` and map
`PandaBear.SharedKernel.*` to it. Map every other package, for example `*`, to the existing source, because
a package without a mapping does not restore once source mapping is enabled. With the SDK pinned in
`global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.RepoConfig.Tool --version <validated-candidate-version>
dotnet tool run repo -- <command>
```

.NET tools run in full trust. Install only from trusted manifests and package sources.

Package ID: `PandaBear.SharedKernel.RepoConfig.Tool`. C# namespace: `SharedKernel.RepoConfig.Tool`.
Assembly/project identity: `SharedKernel.RepoConfig.Tool`. Source is public; registry publication remains
deferred, and nothing here implies availability on nuget.org.

## Usage

```bash
dotnet repo init
dotnet repo verify --root path/to/repository
dotnet repo set github.repository owner/repository
dotnet repo get next-work --limit 5
dotnet repo get by-label "area: tooling"
dotnet repo github reconcile
dotnet repo github reconcile --force
dotnet repo github link-subissue --repository owner/repository --parent 10 --child 20
dotnet repo github link-subissue --repository owner/repository --parent 10 --child 20 --force
dotnet repo github sync-labels --repository owner/repository
dotnet repo github sync-labels --repository owner/repository --force
dotnet repo github sync-labels --repository owner/repository --forgejo https://codeberg.org --force
dotnet repo baseline verify
dotnet repo baseline apply
dotnet repo baseline apply --force
dotnet repo new --root ./example-repo --language dotnet
dotnet repo new --root ./example-repo --language dotnet --feature readme --feature signed-commits --force
dotnet repo automation publish-result --force
dotnet repo verify-podman-dns --verbosity quiet
dotnet repo run-isolated-podman --image-store /absolute/path/to/overlay/store \
    --network-helper slirp4netns -- dotnet test --project tests/Example.Tests
dotnet repo verify-json
dotnet repo verify-line-endings
dotnet repo verify-links
dotnet repo verify-links roadmap/README.md
dotnet repo verify-gherkin
dotnet repo verify-gherkin tests/Features/booking.feature tests/Features/Tours
dotnet repo verify-action-pins
dotnet repo verify-action-pins --manifest .github/action-pins.json
dotnet repo verify-dependabot
dotnet repo verify-target-frameworks
dotnet repo verify-target-frameworks --root path/to/repository
dotnet repo dependency submit --snapshot TestResults/dependency-snapshot.json --repository owner/repository
dotnet repo dependency verify-license-exceptions --github-output "$GITHUB_OUTPUT"
dotnet repo verify-devcontainer --exec "dotnet --version" --exec "git --version"
```

| Command | Purpose |
| --- | --- |
| `init` | Creates missing roadmap structure files and keeps existing ones. GitHub integration starts disabled. |
| `verify`, `diff` | Verify the roadmap structure, configuration, and items; `diff` reports failures as drift. |
| `set <key> <value>` | Sets `github.repository`. |
| `get <query> [<value>]` | Lists roadmap items for `next-priority`, `next-unblocked`, `next-work`, `next-blockers`, `next-enablers`, `low-hanging-fruit`, `pareto`, `blocking-overview`, `blockers-of <item-id>`, `by-tag <tag>`, or `by-label <label>`, and counts for `tags` or `labels`. `--type` filters by roadmap type; `--limit` caps the list at 10 by default. |
| `github sync` | Projects roadmap items to GitHub issues and Projects. |
| `github reconcile` | Reconciles the GitHub snapshot and resolves transferred issue identities. |
| `github import` | Imports the reconciled issue snapshot into roadmap items. |
| `github link-subissue` | Previews or creates a native GitHub parent/subissue link between existing issues in the same repository. |
| `github sync-labels` | Creates the configured labels that are missing and updates those that differ, on GitHub or, with `--forgejo <url>`, on a Forgejo instance; see [Repository labels](#repository-labels). |
| `baseline verify` | Fails when a managed baseline file is missing, outdated, edited by hand, or unmanaged; see [Repository baseline](#repository-baseline). |
| `baseline apply` | Creates and updates the managed baseline files; previews unless `--force` is given. |
| `new` | Starts a repository from a language and features: writes the baseline files and their configuration and initializes Git; previews unless `--force` is given. See [Starting a repository](#starting-a-repository). |
| `seed-drift` | Compares selected write-once files with the current catalog. See [Comparing write-once files](#comparing-write-once-files). |
| `check-tracked` | Runs configured check lanes against nonempty Git-index selections. See [Repository checks](#repository-checks). |
| `verify-commit-signatures --base <ref> --head <ref>` | Verifies the submitted commits with the protected base's SSH signers. See [Verifying signed commits](#verifying-signed-commits). |
| `verify-publication` | Checks a staged policy, Git paths, and index blobs before publication. See [Publication guard](#publication-guard). |
| `automation <step>` | Runs the integrity-bound GitHub Actions steps `plan-github`, `validate-plan`, `apply-plan`, `seal-result`, `verify-result`, and `publish-result`. `publish-result --force` opens the sealed result as a draft pull request; see [Roadmap publication](#roadmap-publication). |
| `verify-text-encoding` | Fails when a text blob in the Git index is not valid UTF-8 or contains NUL. |
| `verify-podman-dns` | Checks local rootless Podman aardvark-dns state without changing it. |
| `run-isolated-podman` | Runs a trusted test command with private local rootless Podman storage and networking. |
| `run-with-budget` | Runs one trusted Linux command while its caller-supplied resource floors hold. |
| `verify-json` | Fails when the working-tree content of a `*.json` file in the Git index is missing, not UTF-8, or not JSON. Comments, trailing commas, and a UTF-8 byte order mark are allowed. Each failure names the path, line, and byte. |
| `verify-line-endings` | Fails when a file in the Git index has CRLF or mixed line endings, staged or in the working tree, while its attributes set `eol=lf`, or LF or mixed line endings in the working tree while they set `eol=crlf`. |
| `verify-links [<path>...]` | Fails on a broken local link or Markdown anchor, a link that leaves the repository, or an issue or pull request reference in documentation files; see [Documentation links](#documentation-links). |
| `verify-gherkin [<path>...]` | Checks tracked Gherkin feature files against the configured rules; see [Gherkin feature files](#gherkin-feature-files). |
| `verify-action-pins [--manifest <path>]` | Fails when an external `uses:` reference in `.github/workflows` or `.github/actions` is not pinned to a full 40-character commit SHA, is absent from the manifest (default `.github/action-pins.json`), pins another SHA than the manifest, or lacks the manifest version as its trailing comment. Local `./` references are skipped, `docker://` references fail, and `firstParty` repositories need only the full SHA. See [reusable workflows](../../docs/ci/reusable-workflows.md#pinned-third-party-actions). |
| `verify-dependabot` | Fails when an `ignore` entry in `.github/dependabot.yml` has no `update-types`. GitHub applies such an entry to security updates as well, so the dependency would never receive a security pull request; list the `version-update:semver-*` types instead. The check is line based and expects block-style YAML. |
| `verify-target-frameworks` | Checks the target frameworks, roll-forward, lock files, pinned SDK, and `dotnet run --project` lines of the tracked project files against the configured policy; see [Target frameworks](#target-frameworks). |
| `verify-test-taxonomy` | Checks declared production and test ownership, test types, project names, and seam registers; see [Test taxonomy](#test-taxonomy). |
| `dependency snapshot` | Generates a dependency snapshot from committed lock files; see [Offline dependency snapshots](#offline-dependency-snapshots). |
| `dependency trial-upgrade` | Tests a NuGet version set in a disposable checkout and writes review artifacts. See [NuGet upgrade trials](#nuget-upgrade-trials). |
| `dependency compare-restore` | Compares restored versions, classifications, dependencies, and selected assets across two checkouts. |
| `dependency inspect-pin` | Reports resolved package ownership and the SDK dependency paths for one package. |
| `dependency submit` | Submits a dependency snapshot to the GitHub dependency graph. |
| `dependency verify-license-exceptions` | Verifies the configured license exceptions; see [License exceptions](#license-exceptions). |
| `verify-devcontainer` | Starts the dev container of the root, runs each `--exec` command in it, and removes it; see [Dev container validation](#dev-container-validation). |

`github` commands, `baseline apply`, `automation publish-result`, and `dependency submit` preview their changes unless
`--force` is given. Lines that describe previewed changes start with `preview:`. The `publish-result`
preview names the repository, base SHA, branch, and changed paths; the `dependency submit` preview names
the snapshot, its size, and the endpoint. Every command except `verify-podman-dns`, `run-isolated-podman`,
`run-with-budget`, and `github link-subissue` takes `--root <path>`,
which defaults to the current directory. A relative root resolves from the current directory, an empty value is a command-line error, and a root that is not an existing directory fails with `--root is not an existing directory: <path>`;
only `init` and `new` accept a root that does not exist yet, which they create. Relative `--snapshot`, `--github-output`, `--log-directory`,
`--devcontainer-cli`, and `verify-links` paths resolve against it. Run `dotnet repo <command> --help` for the
options of each command. `--repository` takes two parts of ASCII letters, digits, `-`, `.`, and `_`, neither of which is `.` or `..`; anything else is a
command-line error. The GitHub commands then also apply GitHub's narrower owner and name rules.

`[diagram]` before a command line prints how the tool parses it without running it, and exits 1 when the
command line does not parse, as in `dotnet repo [diagram] verify-json --root .`.

## Configuration

Commands that apply consumer policy read `.config/sharedkernel-repo.json` under `--root`. The file is
strict JSON of at most 1 MiB with `"version": 1`: property names are camelCase and match exactly, and
comments, trailing commas, and unknown or repeated properties are errors. Paths in it are relative to the
root, use `/`, and have no empty, `.`, or `..` segments; symbolic links are not followed. Errors name the
file and the JSON path of the problem. A command fails when the file lacks its section.

```json
{
  "version": 1,
  "labels": [
    { "name": "type: feature", "color": "a2eeef", "description": "New capability" }
  ],
  "licenseExceptions": {
    "approval": {
      "file": ".config/sharedkernel-candidate.json",
      "values": {
        "packageVersion": "1.0.0-alpha.1",
        "libraryRevision": "0123456789abcdef0123456789abcdef01234567",
        "libraryDirty": false
      }
    },
    "packages": {
      "Example.Package": "2.0.0"
    }
  },
  "links": {
    "include": ["README.md", "docs/*.md"],
    "exclude": ["*/bin/*", "docs/GENERATED*.md"]
  },
  "baseline": {
    "repositoryName": "example",
    "defaultBranch": "main",
    "files": [
      { "path": ".gitattributes", "profile": "gitattributes" },
      { "path": "CODEOWNERS", "source": ".config/baseline/CODEOWNERS" }
    ],
    "gitConfig": [
      { "key": "pull.ff", "value": "only" }
    ],
    "hooks": {
      "directory": ".githooks",
      "hooks": [ { "name": "pre-commit", "profile": "pre-commit-diff-check" } ]
    }
  }
}
```

### License exceptions

`dependency verify-license-exceptions` checks the `licenseExceptions` section, which records package
license exceptions that a review approved:

- Each property in `approval.values` must hold the same string or boolean at the top level of the JSON file
  that `approval.file` names. A change to any approved value, such as a new pinned version, needs a new
  review.
- Each package in `packages` must resolve to its approved version in every target of every
  `packages.lock.json` in the Git index, and at least one of them must resolve it. Package IDs ignore case;
  versions match exactly.

Every finding is listed. When none is found, `--github-output <path>` appends
`packages=pkg:nuget/<id>@<version>,...` in the configured order, the format of the
`allow-dependencies-licenses` input of GitHub's dependency review action; a failure appends nothing.

### Lock-exempt projects

`lockExemptProjects` lists solution projects, as repository-relative project file paths with `/` separators, that keep no
`packages.lock.json`, for example `"lockExemptProjects": ["src/Library/Library.csproj"]`. `dependency snapshot` then needs no lock
for them and writes no manifest for them. A solution project that is not listed must still have a committed lock file, so a
repository that locks every project needs no entry. An entry fails the snapshot when it is not a project of the solution
or when its lock file is committed. List a project only when every package it resolves also appears in the lock file of another retained
project (retain as few locks as that allows); otherwise the snapshot would omit that package. Set `RestorePackagesWithLockFile` to `false` in the listed project
files, because NuGet reports NU1005 for a lock file next to a project with the property false.

### Documentation links

`verify-links` checks the documentation files that the `links` section selects:

- `include` holds at least one path pattern. Without arguments, the command scans each file whose path matches one.
- `exclude` holds path patterns of files that are never scanned, matched after symbolic links resolve; it may be
  empty.
- Patterns match the whole root-relative path with
  `FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false)`: `*` matches any characters, including
  `/`, and `?` matches one character.

Paths given as arguments replace `include` but not `exclude`. Each names a `.md` or `.txt` file, or a directory whose
`*.md` files are scanned recursively, relative to the root; other paths are skipped. Files resolve through symbolic
links, and a file that resolves outside the root is skipped. The walk does not descend into symbolic links to
directories.

Outside fenced code (lines that start with three backticks or tildes), every line, including inline code and HTML
comments, is checked:

- Inline links `[text](target)` end at the first `)`; reference definitions `[name]: target` and autolinks
  `<target>` that hold `://` or start with `#` are checked too. URLs and `mailto:`, `tel:`, and `urn:` targets are
  not followed.
- A local target is percent-decoded, resolves against the file's directory through symbolic links, and must exist
  inside the root. A target that starts with `/` resolves from the file system root, so it leaves the repository.
- A fragment in a link to a Markdown file must name a heading anchor of that file, ignoring case. Every line that
  starts with `#` is a heading. Its anchor is the one GitHub generates, as
  [github-slugger](https://github.com/Flet/github-slugger) reproduces it: the lowercased heading keeps letters of any
  script, marks, decimal and letter numbers, connector punctuation such as `_`, spaces, and `-`, and turns each space
  into `-`, so `ASP.NET Core` becomes `aspnet-core` and a leading emoji leaves a leading `-`. HTML tags are dropped, and
  so are underscores that delimit emphasis outside code spans. A repeated anchor gets the suffix `-1`, `-2`, and so on.
- GitHub issue and pull request URLs, repository-relative `issues/<n>` and `pull/<n>` paths, and bare references
  such as `#123`, `GH-123`, `PR-123`, `issue-123`, and `owner/name#123` are findings. Color values such as
  `fill:#e7f5ff`, URL fragments, and `Unicode Standard Annex #15` or `UAX #15` are not.

Lines, whitespace, lowercasing, and percent-decoding follow Python's string rules, so a UTF-8 byte order mark stays
part of the first line. Each finding names the root-relative path and line of the resolved file; a file that cannot be
read or is not UTF-8 is a finding. On success the command prints how many files it scanned.

### Gherkin feature files

`verify-gherkin` checks the working-tree content of the `*.feature` files in the Git index against the
`gherkin` section, whose properties are all required:

```json
{
  "version": 1,
  "gherkin": {
    "include": ["tests/*"],
    "exclude": ["tests/Generated/*"],
    "allowedTagPatterns": ["@smoke", "@wip", "@ADR:\\d+", "@BC:.+"],
    "forbiddenTags": ["@skip"],
    "featureNameMaxLength": 250,
    "scenarioNameMaxLength": 150
  }
}
```

- Without arguments, the command checks the tracked feature files that an `include` pattern matches. Paths on
  the command line, relative to the root and separated by `/`, replace `include`: each names a tracked feature
  file or a directory that holds one, or the command fails. `exclude` patterns leave files out in both cases.
  Patterns match the whole path case-sensitively, and `*` also matches `/`.
- Each tag must match an `allowedTagPatterns` entry as a whole: the pattern `p` compiles as `\A(?:p)\z` with
  `RegexOptions.NonBacktracking` and a one-second match timeout. A tag in `forbiddenTags` is also reported.
- Name lengths count Unicode code points.

Each finding names the path and line:

- An empty file, or a file that does not end with a newline.
- Trailing whitespace on any line, and a tab on a line that is neither blank nor a comment.
- A tag that is not allowed or is forbidden. Tags are the whitespace-separated words that start with `@` on a line
  that starts with `@`.
- A `Feature:` without a name or with a longer name, a file without a named `Feature:`, and a Feature name that an
  earlier checked file has. Files are checked in ordinal path order.
- A `Scenario:` or `Scenario Outline:` without a name, with a longer name, or with the name of an earlier scenario
  in the file.
- A `Scenario Outline:` without an `Examples:` before the next scenario or the end of the file, reported at that
  line.
- A file without a scenario.

Lines end at LF, CRLF, or CR. A line whose first non-blank character is `#` is a comment, and the text from the
first `#` in any other line is ignored, also inside a name. A keyword counts only at the start of a line and directly
before a colon, or alone, which leaves its name empty. `Example:`, `Scenario Template:`, and `Scenarios:` are not
keywords, so a file whose scenarios all use `Example:` has no scenario. A UTF-8 byte order mark stays part of the
first line, which is then neither a keyword, a tag, nor a comment.

### Test taxonomy

`repo verify-test-taxonomy --root .` validates the tracked `.config/test-taxonomy.json`. The version
1 inventory contains these lists.

| Field | Contents |
| --- | --- |
| `sutRoots`, `testRoots` | Separate root-relative directories containing production projects and test projects. |
| `suts` | Each tracked production project `path`, its `directOwner`, and the `requiredTypes` the inventory expects it to retain. |
| `suites` | Each runnable test project `path`, its tracked Markdown `register`, and its `seams`. |
| `supportProjects` | Paths of test-only helpers, hosts, and contracts that do not themselves run tests. |

Each seam declares a registered `sut`, its `type`, the `dependency` boundary, one or more `capabilities`, and its
CI `lane`. Allowed types are `Unit`, `Component`, `Api`, `Contract`, `Integration`, `System`, `Architecture`,
and `Performance`. Names use `<SUT>.Tests.<Type>`. A mixed or legacy suite can record the reason in
`migrationException`. Inventory paths use `/` separators.

The command checks the Git index against the declared project paths, using the working copy for inventory content.
Stage new files before verification. Run with `--report` for advisory diagnostics during migration. Invalid or
unreadable inventory files and Git failures still return an error.

To include live GitHub Project metadata, pass `--github-project`. This mode reads
`.config/test-taxonomy-project.json` under the same repository root. Supply the
exact Project node ID, owner login and Project number, the required field names and
types, and the explicit repository-qualified issues to verify.

```json
{
  "version": 1,
  "target": { "id": "project-node-id", "owner": "example-owner", "number": 1 },
  "fields": [{ "name": "Coverage owner", "type": "TEXT" }],
  "issues": [{ "repository": "example-owner/example-repository", "number": 27 }]
}
```

The supported types are `TEXT`, `NUMBER`, and `SINGLE_SELECT`. A required issue must
be a Project member and have exactly one value of each required type. Text must
be non-blank. A selection must refer to a current field option. The repository
owns the applicable issues, field choices, and any further value or exception policy.

Use an authorized GitHub credential with Project and issue read access. The
command reads `GH_TOKEN`, then `GITHUB_TOKEN`. A local authenticated gh session
can supply it through `GH_TOKEN="$(gh auth token)" dotnet repo verify-test-taxonomy --github-project`.
The Project check sends only GraphQL queries. Findings remain advisory with
`--report`. Invalid policy, inaccessible metadata, and unusable membership
pagination still return an error. Membership reads stop at 1,000 pages, leaving
room for partially filled pages within GitHub's 50,000-item Project limit.
[GitHub Project limits](https://docs.github.com/en/issues/planning-and-tracking-with-projects/managing-items-in-your-project/adding-items-to-your-project).

### Target frameworks

`verify-target-frameworks` audits the target frameworks of the tracked `*.csproj`, `*.fsproj`, and `*.vbproj` files
against the `targetFrameworks` section without building anything, so that a framework change is an edit of the
policy, a run of the check, and the fixes it reports:

```json
{
  "version": 1,
  "targetFrameworks": {
    "projects": [
      { "include": "src/*.Analyzers/*.csproj", "frameworks": ["netstandard2.0"] },
      { "include": "tools/*.csproj", "frameworks": ["net10.0"], "rollForward": "Major" },
      { "include": "*.csproj", "frameworks": ["net10.0", "net11.0"] }
    ],
    "commandFiles": [".github/*", "*.sh", "*.md"]
  }
}
```

- `projects` is a nonempty list of rules. The first rule whose `include` pattern matches a project applies to it;
  patterns match the whole path case-sensitively, and `*` also matches `/`. `frameworks` is a nonempty list of
  distinct target framework monikers such as `net10.0`, `netstandard2.0`, or `net10.0-windows`. `rollForward` is
  optional: `Minor`, `Major`, `LatestPatch`, `LatestMinor`, `LatestMajor`, or `Disable`, in any case.
- `commandFiles` is an optional list of path patterns of tracked files whose `dotnet run` lines are checked.
- `frameworkProperties` is an optional repository-relative path of an MSBuild file that defines framework properties.
  A rule's `frameworks` entry may then be `$(Name)`, which stands for the `;`-separated list that the property holds
  in that file, evaluated as described below. A reference needs `frameworkProperties`, and a property that the file
  does not define is reported against the policy file, naming the rule. A referenced list must be unique and
  ascending by version, or the file is reported. With `frameworkProperties` set, the check also scans the tracked
  `*.csproj`, `*.fsproj`, `*.vbproj`, `*.props`, `*.targets`, `*.nuspec`, `.github/**/*.yml`, `.github/**/*.yaml`,
  `*.sh`, and `*.ps1` files and the policy file for .NET framework names, `net<major>.<minor>` with an optional
  `-platform` suffix such as `net11.0-windows`, and reports each with its line number. `netstandard` and `netcoreapp`
  names, the `frameworkProperties` file, and the right side of a `'$(TargetFramework)' == '…'` condition are not
  reported. Without `frameworkProperties`, no file is scanned and rules name frameworks literally.

Each finding names the project, lock file, `global.json`, or command file. Projects come first in ordinal path
order, then SDK findings, then command file findings. The command checks, for each project:

- A project that no rule includes is reported, and gets no other check.
- `TargetFramework` and `TargetFrameworks` as the evaluation below finds them. A non-empty `TargetFramework`
  together with a non-empty `TargetFrameworks` is reported, because the SDK cross-targets only when
  `TargetFramework` is empty, so the project builds only that one framework. The fix is to clear it with
  `<TargetFramework></TargetFramework>` where `TargetFrameworks` is set.
- The declared frameworks, which are the `TargetFrameworks` list or else `TargetFramework`, must be the rule's
  `frameworks`, in any order and ignoring case. A project that declares none is reported.
- When the rule sets `rollForward`, the final `RollForward` value must equal it ignoring case. An unset value is
  `Minor`.
- When `packages.lock.json` next to the project is tracked, the frameworks that its top-level `dependencies` keys
  name must be the frameworks that the project builds. A key loses a `/` runtime identifier suffix, and
  `.NETStandard,Version=v2.0`, `.NETCoreApp,Version=v10.0`, and `.NETFramework,Version=v4.8` become
  `netstandard2.0`, `net10.0`, and `net48`.
- When a tracked `global.json` at the root sets `sdk.version`, each framework of the form `netN.M` or `netcoreappN.M`
  with `N` of at least 5 whose major version is above that of the SDK is reported once, with the first project that
  builds it.
- In a tracked file that `commandFiles` selects, a line with `dotnet run` and `--project <path>` is reported when
  the path names a tracked project, or a directory that holds exactly one tracked project file, that builds more
  than one framework, and the line has no `-f` or `--framework`. A line that ends with `\`, or with a backtick
  after an odd number of backticks, continues on the next line. A path with `$`, `{`, `%`, or `<`, or that names
  no tracked project, is skipped.

The properties are read statically, like a local build with no global properties and no environment variables:

- The `Directory.Build.props` chain applies first: the nearest tracked file at or above the project directory, but not
  above `--root`. The project file body follows. For `RollForward` only, the `Directory.Build.targets` chain
  follows that.
- Only top-level `PropertyGroup` elements and `Import` elements are read, in document order. An `Import` whose
  `Project` calls `GetPathOfFileAbove` for the same file name continues with the nearest such file above the
  importing file's directory, at that point; other imports are ignored.
- Property names are case-insensitive, and `$(Name)` becomes the current value or an empty string. Every property
  that is set by an applicable group is tracked, so a central `$(RepoFrameworks)` resolves.
- A `Condition` on a group, a property, or an import supports `'a' == 'b'` and `'a' != 'b'` terms joined by `and` and
  `or`, where `and` binds tighter, without parentheses, comparing ignoring case after substitution. Any other
  condition, such as `Exists(...)`, a property function, or an unquoted operand, skips the assignment. When that
  property is `TargetFramework`, `TargetFrameworks`, or `RollForward` (the last only when a rule checks it), the
  command reports it, and so for a value of those properties that still holds `$(`, `@(`, or `%(`. The project has
  no other findings then. An import with such a condition is followed.
- `Choose`, targets, item metadata, SDK-provided properties, and files outside the Git index are not read.

### Repository labels

`github sync-labels --repository owner/name` reads the `labels` section, a nonempty array of labels:

- `name` has 1 to 50 characters, without control characters or surrounding white space. Names must differ
  ignoring case, because GitHub matches label names ignoring case.
- `color` is six hexadecimal digits without `#`.
- `description` has at most 100 characters without control characters, and may be empty.

The command reads every page of the repository's labels from `https://api.github.com/repos/<owner>/<name>/labels`
and compares them with the configured labels. A missing label is created; a label whose name casing, color, or
description differs is updated. Colors compare ignoring case, and a label without a description matches an
empty `description`. Labels that are not configured are never deleted. Without `--force`, the command prints a
`preview:` line for each label it would create or update and a summary, and writes nothing. With `--force`, it
writes each change and prints the same summary.

Both modes read `GH_TOKEN`, then `GITHUB_TOKEN`; reads need Issues read and `--force` needs Issues write
permission. Requests go only to api.github.com, do not follow redirects, read responses of at most 1 MiB, and
time out after 30 seconds each. Errors report status codes without echoing response bodies. A failed write
names its label; changes written before it remain, and rerunning the command applies the rest.

With `--forgejo <url>`, such as `--forgejo https://codeberg.org`, the command synchronizes the labels of `--repository`
on that Forgejo instance instead, through `<url>/api/v1/repos/<owner>/<name>/labels`. The address must be HTTPS without
user information, a query, or a fragment. A preview reads a public repository without a token; `--force` reads the
token from `FORGEJO_TOKEN` and sends no request without one, and never uses a GitHub token. Forgejo edits a label by
its ID, so an update goes to the ID that the listing read for the label. Colors are sent as `#rrggbb`, as the Forgejo
API documents, and compared without the `#`. The limits, redirects, timeouts, and error reporting are those of GitHub.
The other `github` commands work only with GitHub and have no `--forgejo` option.

## Repository baseline

`baseline verify` and `baseline apply` read the `baseline` section and keep a set of repository files at the
content the configuration produces. The tool names no forge, owner, or host; the consumer picks every path.
`baseline` is unrelated to `security baseline`, which verifies the security documents.

- `repositoryName` (1 to 100 letters, digits, `.`, `-`, `_`) and optional `defaultBranch` (default `main`) are the
  only variables: `@@repositoryName@@` and `@@defaultBranch@@` in content are replaced by plain text. An unknown
  `@@name@@` is an error. Nothing is executed or included.
- `files` is a nonempty array. Each entry has a `path` (repository-relative, no `.git` directory at any depth, no segment that
  contains `~` or ends in `.` or a space, not a JSON file, unique ignoring case) and exactly one of `profile` or `source`. `profile` is an embedded generic file: `gitattributes`,
  `editorconfig`, `gitignore`, `readme`, `pull-request-template`, `issue-form-bug`, or `issue-form-feature`.
  `source` is a repository-relative template file of at most 1 MiB that supplies the content, as for `CODEOWNERS`
  and CI workflow wrappers. `overwriteExisting` (default `false`) allows replacing an existing file that is
  unmanaged or edited by hand.
- Every written file starts with a marker comment (`#`, or `<!-- -->` for `.md`) that holds the SHA-256 of the rest.
  A file is `current`, `missing`, `outdated` (marked, unedited, desired content changed), `modified` (marked, edited
  by hand), or `unmanaged` (no marker, different content). Line-ending differences are not drift; files are written
  with LF.

Embedded profiles, all generic: `gitattributes` (LF policy, CRLF for `.cmd` and `.bat`, binary extensions),
`editorconfig` (UTF-8, LF, final newline, indentation), `gitignore` (environment files, logs, temporary files, OS
and IDE folders), `readme` (skeleton using the two variables), `pull-request-template`, `issue-form-bug`,
`issue-form-feature` (YAML issue forms that GitHub and Forgejo read), `pre-commit-diff-check` (staged whitespace),
`pre-push-diff-check` (pushed tip whitespace), and `commit-msg-conventional` (Conventional Commits).

`gitConfig` is an array of `{key, value, overwriteExisting}` repository-local Git settings, set with
`git config --local` and never by editing `.git/config`. Only these keys are accepted: `init.defaultBranch`,
`pull.ff`, `pull.rebase`, `push.autoSetupRemote`, `push.default`, `fetch.prune`, `rebase.autoStash`,
`commit.gpgsign`, `tag.gpgsign`, `gpg.format`, `user.useConfigOnly`, `core.autocrlf`, and `core.eol`. Keys that can
run code or redirect authentication, such as `core.fsmonitor`, `core.sshCommand`, `core.pager`, `core.editor`,
`credential.*`, `alias.*`, `include*`, and `url.*.insteadOf`, are rejected. A value has 1 to 200 characters, no
control characters, and does not start with `-`. `hooks` has a `directory` (default `.githooks`, a tracked
directory) and `hooks`, each with a `name` (`pre-commit`, `prepare-commit-msg`, `commit-msg`, `post-commit`,
`pre-merge-commit`, `post-merge`, `pre-rebase`, `post-checkout`, or `pre-push`), a `profile` or `source`, and
`overwriteExisting`. Hook files carry the marker after the shebang line and are executable on POSIX systems; a hook
that lost the bit is drift. `apply` sets `core.hooksPath` to the directory, the only way the baseline sets it, and
writes nothing into `.git/hooks`. `overwriteHooksPath` allows replacing a different existing `core.hooksPath`. When
`gitConfig` or `hooks` is present, `--root` must be the top level of a Git repository.

`baseline verify` lists each file that is not current on standard error and exits 1. `baseline apply` prints
`preview:` lines for each file it would create, update, or overwrite and writes with `--force`, in one rollback
batch. A `modified` or `unmanaged` file without `overwriteExisting` is a conflict: it is reported on standard
error, nothing is written, and the command exits 1, in a preview too, because a preview that would fail under
`--force` must not exit 0. Applying an up-to-date repository changes nothing. Missing
parent directories are created, and those that a failed apply created are removed again when empty. Git settings
are set after the files; a failure there leaves the files in place and rerunning is idempotent.

## Starting a repository

`new` starts a repository that has no `.config/sharedkernel-repo.json` yet. It plans a baseline in memory from the
chosen language and features, writes the files and the configuration in one rollback batch, and runs `git init`. Afterwards
`baseline verify` and `baseline apply` work on the repository. See the
[repo new command ADR](../../docs/adr/20261108-repo-new-command.md).

| Option | Meaning |
| --- | --- |
| `--root <path>` | The directory to create or fill. Defaults to the current directory. |
| `--name <name>` | Repository name for the README. Defaults to the last segment of `--root`. |
| `--branch <name>` | Default branch. Defaults to `main`. |
| `--language <id>` | `none` (default), `dotnet`, `shell`, or `nix`. A language selects content for the features you request. |
| `--sdk-version <v>` | The .NET SDK version, such as `10.0.100`, that `global.json` pins and `Directory.Build.props` targets. Needed by the `global-json` and `build-props` features. |
| `--feature <id>` | A feature in the exact selection. Repeat to select several. |
| `--with <id>` | A feature to add to the language defaults. Repeat to add several. Use without `--feature`. |
| `--license-source <path>` | Caller-owned template for `LICENSE`. |
| `--security-source <path>` | Caller-owned template for `SECURITY.md`. |
| `--codeowners-source <path>` | Caller-owned template for `CODEOWNERS`. |
| `--owner <text>` | Value of `@@owner@@` in a policy source. |
| `--copyright-holder <text>` | Value of `@@copyrightHolder@@` in a policy source. |
| `--devcontainer-image <image>` | Caller-selected SHA-256 pinned image for the `devcontainer` feature. |
| `--forge <kind>` | Selects `github` or `forgejo` pull request templates and issue forms. |
| `--ci-policy <path>` | Caller-owned JSON policy for the selected forge's pull request workflow. Requires `--forge`. |
| `--force` | Writes instead of previewing. |

The features are `readme`, `gitignore`, `gitattributes`, `editorconfig`, `hooks` (a tracked `pre-commit` hook and
`core.hooksPath`), `git-config` (`init.defaultBranch`, `pull.ff`, `fetch.prune`), and `signed-commits`
(`commit.gpgsign`, `tag.gpgsign`, `gpg.format=ssh`). Four more features are written once and never managed, because they
have no comment for the marker or are meant to be edited: `agents` (`AGENTS.md`), and with `--language dotnet`
`global-json`, `nuget-config`, and `build-props`. `new` never replaces such a file: if one exists it is a conflict.
`nuget-config` makes nuget.org the only package source, with source mapping, and the only
[audit source](https://learn.microsoft.com/nuget/concepts/auditing-packages#audit-sources), so the audit stays on
nuget.org when a feed is added and NuGet reports `NU1905` when the source provides no vulnerability data.
The standard features for the language and `pre-commit` are selected by default. Use
`--with signed-commits` to keep the defaults and enable signing. Supply a signing key before committing.
Add `--with pre-push-hook` for a local whitespace check at each updated ref tip. It clears inherited Git environment
variables and skips ref deletions. Hosted CI still performs the full verification. Add `--with commit-msg-hook` to run
the Conventional Commit check using the repository's restored `ci` .NET tool. The scaffold installs
selected hooks under the tracked `.githooks` directory and sets `core.hooksPath`.
An unknown language or feature, an invalid name, an existing configuration
file, or a conflict with an existing unmanaged file or different Git setting is reported on standard error and the
command exits 1, in a preview too, with nothing written. The content is neutral: no owner, copyright holder, forge, or
host is embedded.

Each supplied policy source must be a UTF-8 file of at most 1 MiB. Relative source paths are resolved from the
invocation directory. Alongside `@@repositoryName@@` and `@@defaultBranch@@`, sources can use `@@owner@@` and
`@@copyrightHolder@@` with their corresponding options. Use `--owner=@your-team` for a handle with a leading `@`.
A missing source or placeholder value aborts a preview or
write before Git initialization. License, security, and code owner files are written once at the repository root. Source paths and
rendering values are used for the scaffold operation and remain outside the generated baseline configuration.

### Forge templates and CI

Use `--forge github` or `--forge forgejo` to add the managed pull request template and bug and feature forms.
They use the selected forge's directory and form syntax. Use `--ci-policy` to also generate its `workflows/ci.yml`.
The workflow is written once and can be edited by the repository owner. `baseline apply` updates managed files.

```sh
dotnet repo new --root ./example --forge github --ci-policy ./ci-policy.json --force
```

Supply the runner label, a container image with a SHA-256 digest, the checkout and check action URLs, and a timeout
from 1 to 360 minutes. The following uses example pins. Fill in the image and actions approved for your repository.
The checkout action must support `persist-credentials: false`. The check action owns the repository's validation steps.

```json
{
  "runner": "ubuntu-latest",
  "image": "registry.example.test/ci/dotnet@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "githubActionOrigin": "https://github.com",
  "checkoutAction": "https://github.com/example/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd",
  "checkAction": "https://github.com/example/ci/check@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
  "timeoutMinutes": 20
}
```

Each action URL must end with a full lowercase commit SHA. For GitHub, set `githubActionOrigin` to the HTTPS
authority used by your GitHub installation's native action resolver. Both URLs must match it. The generated workflow
uses native `owner/repository[/path]@SHA` references and requests read-only contents.
For Forgejo, omit `githubActionOrigin` and supply absolute HTTPS action URLs reachable from the runner.
The generated check job runs on fork
pull requests, which receive a read-only token and no secrets. Repository owners can add a separate workflow
under their own permissions policy. [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) ·
[Forgejo workflow security](https://forgejo.org/docs/latest/user/actions/reference/)

### Comparing write-once files

```bash
dotnet repo seed-drift --root ./example --feature agents --feature global-json --sdk-version 10.0.100
```

Repeat `--feature` to choose from `agents`, `global-json`, `nuget-config`, `build-props`, `check-runner`,
`publication-guard`, and `signature-verifier`. Pass the expected `--sdk-version` for `global-json` and
`build-props`. Use `--name` when the name for `AGENTS.md` differs from the
directory name. The check compares UTF-8 files up to 1 MiB, ignoring CRLF versus LF and a leading UTF-8 byte order mark.
It reports `current`, `different`, or `missing` with each selected path and returns 1 for differences, missing files,
or input errors. It reads the files without changing them.

### Repository checks

Select `--with check-runner` to create the write-once `.config/sharedkernel-checks.json` file.
When selecting an exact feature list with `--feature`, include a managed baseline feature such as `readme`.
Add the checks the repository needs. Each `pathspec` uses Git's index pathspec rules, and each
`command` array names an executable followed by literal arguments. A lane reads the selected
tracked regular-file paths present in the checkout on standard input as NUL-delimited UTF-8. Lanes run in the Git work
tree root. An empty selection fails before the corresponding executable runs.

```json
{
  "lanes": [
    { "pathspec": "docs/*.md", "command": ["./checks/verify-docs"] }
  ]
}
```

```sh
dotnet repo check-tracked --strict
```

A lane returns zero on success, 77 for tooling unavailable, or another code for a failed check.
Tool absence is a skip by default and a failure with `--strict`. The runner reports every lane
and prints the executed and skipped counts. No executed lane is a failure. Use `--config` to
select another repository-relative configuration. Run these commands only with check files and
executables you trust.

### Verifying signed commits

Select `--with signature-verifier` to create a write-once `.config/allowed_signers` template.
Add the repository's authorized SSH public keys using the
[OpenSSH allowed signers format](https://man.openbsd.org/ssh-keygen#ALLOWED_SIGNERS), then commit the file
to the protected branch before requiring the check.

Run a trusted, pinned installation of the tool from a checkout with complete history. Give it the protected
base and actual change head from the forge event. The event's head identifies the submitted commits and excludes the forge's synthetic
test merge from the range.

```sh
dotnet repo verify-commit-signatures --base "$BASE_COMMIT" --head "$CHANGE_HEAD"
```

The tool reads the protected base's signer file directly from Git and verifies every commit in
`base..head`. An empty range, an unavailable ref or signer file, or an unverified commit fails the check.
Key lifetime options such as `valid-before` apply at the recorded commit time. The check requires
Git with SSH signature support and OpenSSH 8.8 or newer for key lifetime options.

### Publication guard

Select `dotnet repo new --with publication-guard --force` to seed an owner-editable
`.config/publication-guard.json` policy and an ignore entry for
`.config/publication-guard.local.patterns`. Supply the repository's rules, then stage the
policy and the files to check. Run `dotnet repo verify-publication` from the repository.
Use `--root <directory>` to select a working tree.

```json
{
  "version": 1,
  "neverTrack": [":(top,glob)private/**"],
  "patterns": [{"id": "example-mailbox", "regex": "@example[.]invalid"}],
  "allowlist": [{"rule": "example-mailbox", "path": "docs/example.md", "reason": "Synthetic example"}]
}
```

`neverTrack` uses Git pathspecs. Pattern rules use Git extended regular expressions and
check staged bytes, paths, and symlink targets. The staged policy is excluded from its
configured patterns so it can declare those patterns. A reasoned allowlist entry exempts
one exact path from one named pattern. It has no effect on a forbidden path.

Put additional private patterns in the ignored local file, one expression per line.
Blank lines and lines starting with `#` after optional whitespace are ignored. The local
file remains untracked. Its absence or an empty pattern set is reported as a skip.
Diagnostics report the failing check without revealing pattern values, paths or matched
content. The check covers the staged index. Publication reviews that include existing
history need a separate history inspection.

### Verifying the pinned .NET SDK

With `--language dotnet`, select `--with sdk-bootstrap` to write `scripts/bootstrap-dotnet.sh`.
It reads the SDK version from `global.json` and checks for the exact installed SDK by default.
An exact feature selection needs an existing `global.json` or the `global-json` feature.
The launcher's prerequisites are a POSIX shell and `jq`.

```sh
sh scripts/bootstrap-dotnet.sh
sh scripts/bootstrap-dotnet.sh --install
```

Explicit installation uses Microsoft's release metadata to select the SDK and platform, checks
the exact HTTPS URL and verifies the published SHA-512 before extraction. It uses `curl`,
`sha512sum` or `shasum`, and `tar` or `unzip`. The verified host is installed side by side
under `bin/sharedkernel-dotnet/<version>/<rid>`. Use the printed host path to select that SDK.
[Microsoft .NET release metadata](https://github.com/dotnet/core/blob/main/release-notes/releases-index.json)

### Shell, Nix and Dev Containers

`--language shell` sets LF attributes for shell source and Bats files. `--language nix` ignores
Nix build-result links while keeping source and lock files trackable. The .NET-only features
require `--language dotnet`.

Add `--with devcontainer` and pass a caller-selected image reference via `--devcontainer-image`.
The image must use an `@sha256:` digest with 64 lowercase hexadecimal digits. Creation validates
the feature and image together before writing files. The repository owns subsequent changes to
the seeded `.devcontainer/devcontainer.json`. With an exact feature list, select a managed baseline
feature as well.

## Roadmap publication

`automation publish-result` publishes the sealed roadmap maintenance result. The draft pull request's body is
`automation.pullRequestBody` from `roadmap/config.json`, a nonblank string of at most 65536 characters, and the
command fails without it. `init` writes the property and its schema; set the body that the repository's pull
request policy requires, such as an issue reference:

```json
"automation": {
  "pullRequestBody": "Refs #123"
}
```

With `--force`, the command requires `GITHUB_OUTPUT` and `GITHUB_STEP_SUMMARY` before it writes to GitHub. It
succeeds only when GitHub returns the draft pull request's URL, which it appends to `GITHUB_OUTPUT` as
`pull_request_url` and to the step summary.

## Transferred issues and external references

A roadmap item's `id` stays stable when its GitHub issue moves. Existing numeric mappings still use
`roadmap/config.json`'s configured repository. Optional item-level fields identify a different current
location and retain historical aliases:

```json
"integrations": {
  "github": {
    "issue": 18,
    "repository": "owner/shared-library",
    "nodeId": "opaque-current-node-id",
    "previousLocations": [{ "repository": "owner/application", "issue": 457 }],
    "previousNodeIds": ["opaque-previous-node-id"]
  }
}
```

These are schema examples; populate identities from GitHub. Node IDs are opaque references and may
change format; do not decode them. Previous locations and node IDs preserve identity across transfers
and ID migrations. Existing aliases do not create new roadmap items. Creation intent
(`"issue": "create"`) cannot include existing node IDs or historical aliases.

`github reconcile` previews resolved locations, aliases, and snapshot changes without writing.
`github reconcile --force` applies the reconciliation manifest and accepted identity updates atomically.
Transfers preserve roadmap IDs, filenames, local status, priority, and relationships. External mappings
are read-only: `github sync` skips their issue and Project mutations. Parent and blocker links continue
to refer to roadmap IDs across repository boundaries.

Cross-repository blockers use the manifest's optional `externalBlockerEdges` array. Each `blocker` and
`blocked` endpoint records `repository`, `issue`, and `state`; exactly one endpoint belongs to the
configured repository. Local numeric `blockerEdges` remain compatible. External edges come from
explicit GitHub blocker metadata, are bound to the accepted snapshot digest and integrity count, and
map to existing qualified roadmap identities without inventing external items. Intake retains both
`blocks` and `blockedBy` links for mapped endpoints; unresolved endpoints require review before
changing affected relationships.

Missing or inaccessible references produce diagnostics; they do not establish closure or deletion.
Unresolved metadata preserves accepted local relationships rather than erasing them. Review affected
references before applying changes, and use explicit closure approvals for actual closed-item transitions.

Each `closedItemTransitions` entry may carry `"closeReason": "NOT_PLANNED"`, the GitHub close outcome. Planning reads
`stateReason` from GitHub and records the outcome in the plan, so the reviewer sees which closures were not completed;
`apply-plan` writes `closeReason` only when it is `NOT_PLANNED`, and an entry without it means completed. On import, a
`NOT_PLANNED` transition sets the item to `integrations.github.intake.notPlannedStatus`, which must be a configured closed
status (for example `dropped`); import fails when that setting is missing. Other transitions use `closedStatus`.

An approved plan is bound to its repository inputs, not to commit SHAs. `validate-plan` and `apply-plan` accept a newer
default-branch head when the checkout is that head and the digest of `roadmap/config.json`, `roadmap/order.json`, items,
themes, and reconciliation manifests is unchanged; otherwise they fail with the stale-input error. `seal-result` records
the head it was sealed on, and `publish-result` opens the pull request against that head. The workflow must therefore
check out the current default branch for the apply job. The GitHub snapshot read repeats up to three times when only the
default-branch commit moved while it was read.

## Environment

| Variable | Read by |
| --- | --- |
| `GH_TOKEN`, then `GITHUB_TOKEN` | `github` commands and `dependency submit --force`. |
| `FORGEJO_TOKEN` | `github sync-labels --forgejo <url>`; required with `--force`. |
| `GITHUB_TOKEN` | `automation` steps that call GitHub. |
| `GITHUB_REPOSITORY` | `automation` steps. |
| `GITHUB_RUN_ID` | `automation plan-github`. |
| `ROADMAP_PLAN_DIGEST`, `ROADMAP_ARTIFACT_DIGEST`, `ROADMAP_RESULT_DIGEST` | `automation` steps after planning, as each step requires. |
| `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | `automation plan-github`, `seal-result`, and `publish-result --force`. |

## Isolated Podman test runs

`run-isolated-podman` is an opt-in wrapper for trusted commands that use the local Podman CLI.
It requires Linux rootless Podman with netavark, overlay storage, and an existing additional image
store. Find the normal rootless overlay store with
`podman --remote=false info --format '{{.Store.GraphRoot}}'` or supply a separately provisioned store.
Podman reads the supplied image store through its read-only `additionalimagestores` configuration.
Each invocation creates its writable graph, runtime, engine state, and networks under its own temporary directory.
On a host that restricts the rootless network helper to the user runtime directory, set
`--runtime-parent "$XDG_RUNTIME_DIR"` while keeping `TMPDIR` on disk. The wrapper creates an
owner-only runtime directory under the supplied existing absolute directory for the runroot,
network configuration, and transient libpod state. The graph, volumes, static libpod state and
the child's temporary path stay under the caller's normal disk-backed temporary location.
The caller's runtime parent is preserved, and the engine checks and cleans up both private roots.
The child inherits the user runtime directory and session bus and receives `ASPIRE_CONTAINER_RUNTIME=podman`.
Ambient Docker and Podman endpoint overrides and forge tokens are removed from the child environment.
The rootless network helper defaults to Pasta. On a host where the packaged security policy prevents
Podman from stopping Pasta during teardown, use `--network-helper slirp4netns` if that helper is installed.
Pass `--` before the child executable so the wrapper forwards the remaining arguments verbatim.
The child runs in the current directory and has a default timeout of 1,800 seconds. Use
`--timeout-seconds` to change the timeout. The wrapper captures bounded output and returns the child's exit code.

For an aggregate memory budget with rootless systemd Podman on Linux cgroup v2,
create a caller-owned user slice with finite `memory.max` and `memory.swap.max`.
Launch the wrapper in a scope below that slice and pass the slice name to
`--cgroup-parent-slice`. The child must pass the same name to `--cgroup-parent`
when creating each Podman container. The wrapper verifies the active slice and
the effective limits and ancestry of live private payload and conmon processes
it observes. A run that cannot establish placement fails. Keep one or more
owned containers running long enough to be observed. The caller manages the
slice and supplies its runtime arguments. The `run-with-budget` host and
scratch checks can run alongside placement verification.
[Podman cgroup parent][repo-podman-cgroup-parent] ·
[systemd resource control][repo-systemd-resource-control]

[repo-podman-cgroup-parent]: https://docs.podman.io/en/v5.7.0/markdown/podman-run.1.html#cgroup-parent-path
[repo-systemd-resource-control]: https://www.freedesktop.org/software/systemd/man/latest/systemd.resource-control.html

The default console mode captures up to 8,388,608 characters from each child stream. It stops the
child if either stream exceeds the limit. Redirecting the wrapper's own output keeps this console limit.
For verbose runs, supply an existing caller-owned absolute directory outside the private engine
paths and choose a positive byte limit for each file.

```sh
dotnet repo run-isolated-podman --image-store /absolute/path/to/overlay/store \
    --log-directory /absolute/path/to/test-logs --max-log-bytes 67108864 \
    -- dotnet test --project tests/Example.Tests
```

The wrapper creates new `stdout.log` and `stderr.log` files and copies the child streams as bytes.
Existing files are preserved and a collision fails before the engine starts. Engine diagnostics still
go to the wrapper's standard error. Partial logs survive child failure, timeout, cancellation, or
output overflow. A write failure or a stream that exceeds its byte limit stops the child and cleans
up the verified engine. Reserve space for two files at the chosen limit.

The wrapper verifies engine ownership and DNS before the child starts. After the child exits or is
canceled, it verifies ownership again, removes resources in the private engine, and checks empty
container and network state and healthy DNS. Podman removes writable images from the verified
private store before the wrapper deletes its temporary directory. The shared image store stays
read-only. If the second ownership check or teardown fails, the wrapper
reports the retained private directories and returns a failure. It only removes resources from the
verified private engine. The wrapper allows up to two minutes for cleanup and up to three minutes to
shut down after Ctrl+C or SIGTERM. Tests using a Docker API socket need a separate setup.

## Command resource budgets

`run-with-budget` checks the caller's Linux host memory and scratch filesystem before the command starts and
periodically while it runs. Supply existing absolute working and scratch directories and the absolute path for
a new report file. Put `--` before the trusted executable and its literal arguments. The child inherits the
invoking environment and console, with `TMPDIR` set to the selected scratch directory. Tools that ignore
`TMPDIR` need their own scratch configuration. These example floor values belong to the caller.

```sh
scratch=/absolute/path/to/scratch
report=/absolute/path/to/new-budget.json
dotnet repo run-with-budget --working-directory "$PWD" --scratch "$scratch" --report "$report" --minimum-host-memory-mib 8192 --minimum-scratch-mib 16384 --sample-seconds 10 -- dotnet test
```

The required floors use Linux `MemAvailable` and available bytes on the filesystem containing the scratch
directory. The initial and latest samples also report host `Shmem`, scratch filesystem type, and cgroup-v2
headroom where measurable. Opt into `--reject-memory-filesystem` to require a recognized type and reject
`tmpfs` or `ramfs`. A supplied `--minimum-cgroup-headroom-mib` is an independent floor and requires a finite,
readable cap in the current inherited cgroup hierarchy. It uses the smallest remaining `memory.max` minus
`memory.current` among visible finite limits on the standard unified mount. Current usage includes charged
page cache. Without this option, cgroup measurements are diagnostic. The wrapper leaves limits and cgroup placement
to the caller. [Linux cgroup v2 memory controller](https://www.kernel.org/doc/html/v6.10/admin-guide/cgroup-v2.html)

`--maximum-memory-pressure-percent` opts into stopping when host memory PSI `some avg10` is above the chosen
percentage for consecutive observed samples, including preflight. `--pressure-samples` defaults to two. A sample
at or below the pressure limit resets the streak. A required measurement becoming unavailable, a floor crossing,
or caller cancellation stops the owned command. [Linux pressure stall information](https://docs.kernel.org/accounting/psi.html)

The bounded JSON report contains the supplied floors, initial and latest measurements, sample count, child
exit status or guard reason, and cleanup findings. It omits command arguments, environment values, path names,
and child output. A natural exit returns the child's status. A guard refusal, stop, or launch failure returns
125, and observed caller cancellation returns 130. The report distinguishes these outcomes even if a child
uses the same exit code.

After a guard stop, the tool requests termination of the owned process tree and checks the process identities
it could observe. The report records incomplete observation or remaining observed processes. Polling can miss a
resource spike between samples. An inherited cgroup cap applies only while processes stay in that hierarchy, and
the process tree check cannot account for work that detached or was reparented before observation. Configure
separately scoped engine services and containers under their owning lifecycle. To include them in a cgroup
budget, their owner must set the engine's cgroup parent accordingly and retain the host floor.

Use `--cooperative-podman-cleanup` when the guarded command runs
`run-isolated-podman` directly or through a trusted recipe. Both wrappers need to
support the cleanup protocol. The guard creates an owner-only rendezvous in
the writable scratch directory and passes it in the command environment.
Intermediaries that launch the isolated wrapper preserve that environment.
At a guard stop, active private engines have five seconds to acknowledge
cancellation, then up to 140 seconds to complete verified teardown. A later
isolated step sees the stop before opening a workspace. The guard uses its
owned process-tree fallback when these windows expire or no engine has enrolled.

The JSON report keeps the original stop reason and exit status. The additional
`cleanup.isolatedPodmanTeardown` field reports `Completed`, `NoActiveEngine`,
`Retained`, `Unacknowledged`, `TimedOut` or `Unavailable` separately from the
process observation. The isolated wrapper's error output identifies retained
workspaces. Keep the caller-owned scope and its hard memory and swap caps in
place through cleanup. The caller retires the scope, slice and scratch after
verifying the owned processes and private runtime paths. An incomplete or
unverified engine teardown retains the rendezvous for diagnosis.

## Limits

- `verify-podman-dns` inspects only local rootless Podman on Linux and fails closed for remote services.
- `verify-text-encoding` checks staged regular files, skips files whose `binary` attribute is set, and
  fails on blobs larger than 64 MiB.
- `verify-gherkin` reads the working-tree content of the regular files in the Git index, skips symbolic links,
  and fails on files larger than 64 MiB. Untracked feature files are not checked, and a tracked file missing from
  the working tree is reported.
- `verify-json` reads the working-tree content of the regular files in the Git index, skips symbolic links
  and submodules, and fails on files larger than 64 MiB. In a sparse checkout, files outside it are
  reported as missing.
- `verify-line-endings` reads the line endings that `git ls-files --eol` reports. Git stores `eol=crlf` text
  with LF in the index, so only the working tree is checked for it. Files missing from the working tree are
  checked only in the index.
- `verify-links` reads the working tree, not the Git index, and fails on files larger than 64 MiB.
- `verify-target-frameworks` reads the working-tree content of the regular files in the Git index, skips files it cannot read in command files, and reports a project or props file larger than 4 MiB or another file larger than 16 MiB.
- `dependency verify-license-exceptions` reads the working-tree content of the regular `packages.lock.json`
  files in the Git index, and fails on an approval file larger than 1 MiB or a lock file larger than 64 MiB.
- `dependency submit` accepts a snapshot of at most 32 MiB, the size that `dependency snapshot` writes.
- `verify-devcontainer` bootstraps its CLI only on Linux x64 and accepts downloads of at most 256 MiB. A
  container that `devcontainer up` creates before it reports its ID, or that outlives the two-second
  termination timeout after Ctrl+C or SIGTERM, is not removed.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success, including previews and a healthy or inapplicable Podman state. |
| 1 | Verification failure or drift, invalid text encoding or JSON, a line-ending, link, Gherkin, or target framework finding, a license exception finding, baseline drift or a baseline conflict (also in a preview), stale or unverifiable Podman DNS state, invalid configuration, a file, Git, or GitHub error, a rejected dependency submission, a failed dev container start, command, or CLI bootstrap, a command-line error, or cancellation. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or the command did not stop within its shutdown window. |

`run-isolated-podman` also returns any nonzero child exit code.

## Security

- Tokens come only from the environment. GitHub transport failures are reported without request details.
- Child processes (Git, Podman, `stat`, Docker, and the Dev Container CLI) start without `GH_TOKEN`, `GITHUB_TOKEN`,
  `FORGEJO_TOKEN`, `GH_ENTERPRISE_TOKEN`, and `GITHUB_ENTERPRISE_TOKEN`.
- Git and Docker start from the first absolute `PATH` entry that holds them, never from the current directory
  or a relative or empty `PATH` entry.
- Git starts with `GIT_TERMINAL_PROMPT=0`, without `SSH_ASKPASS` or any inherited `GIT_*` variable, with bounded
  output, and with 30 seconds for each call.
- `verify-devcontainer` checks each downloaded archive against the hash pinned in the tool before reading it,
  and extracts only regular files to fixed paths: `bin/node` and the entries under `package/`. Links and
  entries with empty, `.`, or `..` segments fail the bootstrap.
- Without `--force`, `github` commands and `automation publish-result` only read and change nothing in
  GitHub or the checkout; `--force` never skips validation.
- `dependency submit` sends no request and reads no token without `--force`. With it, the command sends
  the snapshot file unchanged and reports only the HTTP status of a rejected request.
- `automation` steps after planning are bound to the reviewed plan digest. `publish-result` requires a
  sealed result with or without `--force`, and its preview reads GitHub only to validate. With `--force`
  it creates Git objects, one new branch, and a draft pull request. When the branch already exists it
  fails and leaves only unreferenced Git objects.
- The `publish-result` preview prints no file contents.
- Output escapes control characters from roadmap and GitHub data.

## Dev container validation

`verify-devcontainer` validates `.devcontainer/devcontainer.json` under the root with the Dev Container CLI:

1. Without `--devcontainer-cli <path>`, it uses `@devcontainers/cli` 0.84.1 on Node.js 24.22.1, cached under
   `TestResults/devcontainer-tools/devcontainers-cli-0.84.1-node-24.22.1`. A missing cache is downloaded from
   `nodejs.org` and `registry.npmjs.org`; the Node.js archive must match the SHA-256 in the release's signed
   `SHASUMS256.txt.asc`, and the CLI tarball the registry's `dist.integrity`, both pinned in the tool.
2. It runs `devcontainer up --workspace-folder <root> --log-level trace`. When `.git` under the root is a file,
   as in a linked worktree, it adds a bind mount of the Git common directory at the same path, so Git works in
   the container.
3. The CLI output appears on standard output as it arrives and is written to `devcontainer-up.log` in the log
   directory, `TestResults/devcontainer-smoke` unless `--log-directory <directory>` names another. The command
   fails when the CLI exits with a non-zero code or its output contains `"outcome":"error"`, even with exit
   code 0.
4. The last `"containerId":"<id>"` on standard output names the container, or, without one there, the last in the
   log. With an ID, `metadata.txt` in the log
   directory records `workspace_folder`, `log_dir`, `devcontainer_cli`, and `container_id`; without one, the
   command fails.
5. Each `--exec <command>` runs in order as `devcontainer exec --workspace-folder <root> bash -lc <command>`, with
   its output on standard output and in `devcontainer-exec-<n>.log`. The first non-zero exit code fails the
   command.
6. Afterwards, also after a failure or cancellation, `docker rm -f <id>` removes the container unless
   `--keep-container` is given. A failed removal is reported and does not change the exit code.

```bash
dotnet repo verify-devcontainer --devcontainer-cli /usr/local/bin/devcontainer --keep-container --root .
```

## Security commands

`security baseline --root <path>` verifies the configured repository document contracts.
`security scan --root <path> --output <directory>` also scans current source with Gitleaks.
See the [security baseline](../../docs/security-baseline.md) for configuration and exit codes.

## NuGet upgrade trials

Use `dependency trial-upgrade` to test a coordinated package change under the repository's pinned .NET SDK.
Select the central file that owns the declarations, including an imported file, with `--props`. Repeat
`--update package@version` for each member of the candidate set. `--remove-pin` removes a single
transitive declaration only when the evaluated solution and tracked file-based apps allow that
declaration to be removed. A file-based app can use its nearest central file or an imported version.

```sh
repo dependency trial-upgrade --props eng/Versions.props --remove-pin Contoso.Driver.Core --update Contoso.Driver.PostgreSql@2.0.0 --update Contoso.Driver.Hosting@2.0.0 --plan TestResults/nuget-upgrade.patch
```

Use `--propose` with `--remove-pin` to ask the SDK for updates of the direct owners of the pin.
Add `--coordinate <package>` for each additional direct package in a coupled family.
The report lists the selected direct IDs and shows the SDK's chosen versions in the verified graph.
The SDK chooses the highest version available for each selected ID from the configured package sources.
Use exact `--update` versions when you need a different compatible set.

```sh
repo dependency trial-upgrade --props eng/Versions.props --remove-pin Contoso.Driver.Core --propose --coordinate Contoso.Driver.Hosting --plan TestResults/nuget-proposal.patch
```

The trial snapshots the current revision and tracked working changes into a disposable checkout. For a mapped
repository-relative feed that is absent from Git, pass its directory to `--copy-local-source`. The trial copies
its package archives to the same relative location and uses the repository's existing `NuGet.Config`. Other
untracked files stay in the source checkout.

Locked baseline restore, candidate restore and locked candidate restore cover all evaluated projects, including
projects that intentionally keep no lock file. The trial restores tracked file-based apps in the disposable
checkout and includes their resolved dependencies in the report. It withholds the patch when their restore
evidence is incomplete or when they reference other MSBuild projects. Each app uses its tracked lock or
a separate scratch lock when repository settings enable locking without a tracked file. Add `--build` for a Release solution build or `--test` for the
Release build and full tests. The JSON report has the restore comparison, SDK diagnostics, verification outcomes
and the patch digest. Where removing a pin would lower or leave an unconfirmed version floor, the report shows
the projects and targets where the graph changed. The trial writes a report and withholds the patch.

Use `--vulnerable` to find a minimum upgrade for reported advisories. The search includes directly
affected packages and direct owners of transitive dependency paths. Use `--coordinate <package>` for
other direct members of a coupled family. Each considered owner must have one literal version
declaration in the selected `--props` file. The search considers upgrades above every current direct
resolution. It considers stable releases unless a scope already resolves a prerelease.

The search orders exact sets by the number of changed direct declarations, then the case-insensitive
sequence of package IDs, then ascending NuGet versions in that order. It evaluates at most 12 owners,
512 relevant versions per owner and 32 sets. Each attempt starts from the same tracked state, performs
fresh restore under the original audit policy, and verifies the selected versions and locked restore.
The trial keeps the strict baseline result when advisory errors require a diagnostic warning-only
restore to read the initial graph. The final candidate uses the original severities and suppressions.

A successful earlier set proves the minimum only within the listed owners and eligible releases.
Missing or ambiguous source data, an indeterminate earlier restore, or a search limit produces
`minimumUnproven` with evidence and no patch. A source availability diagnostic prevents a minimum claim
even if the repository allows the restore to report it as a warning. The report separates unavailable or partial package
metadata from an exhaustive set that cannot resolve the advisories. Use explicit `--update` versions
to verify another reviewed set when a complete automatic proposal is unavailable.

The trial builds a managed metadata resolver in a temporary directory using the checkout's selected
.NET 10 or 11 SDK. The resolver uses that SDK's NuGet settings, package source mappings and
noninteractive credential providers. The tool's packaged and NativeAOT builds carry the resolver
sources and use the installed SDK at runtime. Before release, run the mapped audit trial on Windows
alongside the hosted package and NativeAOT consumption checks.

Review the JSON and the patch, then verify and apply it with Git. Use a fresh output path for the next trial.

```sh
git apply --check TestResults/nuget-upgrade.patch
git apply TestResults/nuget-upgrade.patch
```

## Offline dependency snapshots

Generate a GitHub dependency-submission snapshot from the exact clean reviewed commit, without restoring,
loading, or downloading its packages:

```sh
dotnet repo dependency snapshot --root /path/to/reviewed-checkout --sha <commit-sha> --ref refs/heads/main --job <run-id> --correlator solution-nuget --output TestResults/dependency-snapshot.json
```

The command reads the committed Git tree and blobs at the reviewed SHA, rather than trusting working-tree
files or Git's status cache. It inventories every project in the root's unique `.slnx` and requires its
committed `packages.lock.json` in the supported version 1 or 2 layout, except for the projects that
`lockExemptProjects` in `.config/sharedkernel-repo.json` lists (see [Lock-exempt projects](#lock-exempt-projects)). Git lazy fetching and replacement
objects are disabled for these reads. Input blobs are limited to 8 MiB each and 64 MiB in aggregate.
Snapshot JSON is limited to 32 MiB; the budget is checked while writing package nodes and edges,
before an amplified graph can be published.

Snapshots contain only genuine NuGet package URLs. Every framework, exact resolved version, direct/transitive
classification, and package edge is retained. Local Project nodes are connectors: any package dependency
through them is expanded to its real package targets, without inventing external project packages.
Centrally pinned transitive packages remain indirect. Project cycles are rejected, traversal is iterative,
and dependency expansion is limited to one million edge visits. Package scope is conservatively `runtime`
because lockfiles do not encode development-only asset usage.

Missing committed locks, unresolved edges, ambiguous names, unsupported node kinds or lock versions,
dirty/mislabeled commits, and unsafe output paths fail before publication. Existing output is not overwritten.
The command itself performs no network calls. One source-built tool can inspect both historical base and
current head checkouts, so retired releases are unnecessary for dependency review. Actual application builds
still require the current candidate's artifacts.

### Compare evaluated restores

Run an audited locked restore in each checkout using its pinned SDK and configured package sources.
Pass the two checkouts to the comparison command.

```sh
repo dependency compare-restore --root /path/to/baseline --candidate /path/to/candidate --output TestResults/restore-graph.json
```

The command asks each selected SDK to evaluate the solution's restore inputs and compares them
with the restored assets and successful restore cache. It reports stale or missing evidence per
project and exits with status 1 if any project is unavailable. The comparison includes projects
whose evaluated settings disable lock files, plus separate target entries for runtime identifiers.
Tracked file-based C# apps with build or dependency directives appear under their source paths.
When their current assets are missing or outside the checkout, the report marks that app's graph
unavailable and the command returns status 1. The comparison uses SDK project evaluation and existing restore assets.
The report includes both Git revisions, dirty flags, SDK versions, and package additions,
removals, or changes to versions, requested dependencies, content hashes, classifications,
and selected asset paths. To record build and test outcomes, pass an existing CI execution
manifest from `ci run` as `--baseline-verification` or `--candidate-verification`.
Each manifest must match the exact clean checkout and is resolved inside that checkout.
The report retains the named validation slices and their restore, build, and test outcomes.
Use a fresh output path inside the baseline checkout.

### Inspect a transitive pin

After locked restore, inspect a package before trying to update or remove its central pin.

```sh
repo dependency inspect-pin --package Microsoft.Extensions.Logging
```

The report names the checkout revision, SDK, and each project, framework, and RID selecting the
package. It includes direct, central transitive, or transitive classification and whether the project
uses a lock file. The SDK dependency tree identifies which direct packages and project references
requested it. The command leaves package declarations and lock files untouched and returns status 1
when a restored project or its SDK path report is unavailable.
For tracked file-based apps, the SDK evaluates their requested direct packages and names the
file that defines each central version. That declaration can come from an imported props file
or a nearer `Directory.Packages.props`. File-app declarations in this report describe requests.
The SDK dependency tree describes resolved solution projects.

`dependency submit` sends the snapshot to GitHub's dependency submission API:

```sh
dotnet repo dependency submit --snapshot TestResults/dependency-snapshot.json --repository owner/repository --force
```

The snapshot must be a JSON object, and `--repository` must name `owner/name`. With `--force`, the command
posts the file unchanged to `https://api.github.com/repos/<owner>/<name>/dependency-graph/snapshots` with the
token from `GH_TOKEN`, then `GITHUB_TOKEN`; a workflow token needs `contents: write`. A response other than
2xx, or a result of `INVALID`, fails the command.

## Native GitHub subissue links

`github link-subissue --repository owner/repository --parent 10 --child 20` validates the repository
name and positive issue numbers and previews the intended relationship without network access or
credentials. It requires no roadmap or consumer configuration.

Add `--force` to resolve both existing issues, check the child's current parent, create the native
link using the child's numeric REST identity, and verify its parent afterward. An already matching
relationship succeeds without a write. A different parent is rejected; `--force` never reparents.
The command supports issues in one repository on github.com; pull requests are rejected.

Forced linking reads `GH_TOKEN`, then `GITHUB_TOKEN`, and requires access to both issues with Issues
write permission for mutation. It does not invoke `gh` or extract credentials from another process.
Supply the token through the existing environment authentication flow; never pass it as an argument.
Requests go only to api.github.com, do not follow redirects, and bound issue responses to 1 MiB and
the complete operation to 30 seconds. Cancellation is propagated. Errors report status codes without
echoing remote response bodies or transport details.

A successful POST followed by failed verification, or cancellation/transport failure during a write,
may have changed the remote relationship; inspect the child before retrying. Writes are not automatically
retried. A 422 response is accepted only
when a readback proves that a concurrent request created the exact requested link. Exit code 0
means previewed, already linked, or linked and verified; validation, API, verification, and timeout
failures exit 1. No package publication or consumer package adoption is performed by this command.

The API contract follows [GitHub's subissue REST documentation](https://docs.github.com/en/rest/issues/sub-issues).
