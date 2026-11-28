# PandaBear.SharedKernel.Packages

A model of the packages on a machine with validated names, versions, checksum pins, and package sets.
`dotnet syspackages` reads `.config/sharedkernel-packages.json`, compares installed packages with the
declaration, and checks update candidates against advisories and a minimum release age. A host can also
reconcile a declaration through execution ports it supplies. Targets .NET 11.

```csharp
using SharedKernel.Packages;
using SharedKernel.Processes;

var configuration = await PackagesConfiguration.Read(repositoryRoot, ct);
var inventory = new PackageInventory(new BoundedProcessRunner(), PathExecutableResolver.Default);

var plan = await inventory.Plan(configuration.Packages, ct);
foreach (var manager in plan.Managers)
{
    foreach (var change in manager.Changes)
    {
        Console.WriteLine($"{PackageManagers.Id(manager.Manager)}: {change}");
    }
}

var updates = await inventory.CheckUpdates(
    configuration.Packages, configuration.MinimumReleaseAge ?? MinimumReleaseAge.FromDays(7).Value,
    NetworkAccess.Allowed, UpdateSources.CreateDefault, ct);
```

- `PackagesConfiguration.Read` reads `.config/sharedkernel-packages.json` under a root through
  `PandaBear.SharedKernel.ConfigurationFiles` (at most 1 MiB, no symbolic links, strict source-generated JSON) and
  validates it: version 1, known manager IDs, `name` or `name=version` entries, one owner per package, and an optional
  `minimumReleaseAgeDays` from 0 to 3650. Any problem throws `InvalidOperationException` whose message names the file
  and the JSON path.
- `PackageInventory` takes an `IProcessRunner` and an `IExecutableResolver`. The host decides how to display its reports
  and which exit codes they map to. `Plan` finds every manager's listing
  program first and runs nothing when one is missing (`PackagePlanReport.MissingTools`); otherwise each `ManagerPlan`
  carries its changes or the reason its installed list could not be read. `CheckUpdates` judges the declared, installed,
  unpinned cargo and npm packages; other managers are `Unsupported`, pinned packages and packages the registry does not
  know are listed per `ManagerUpdates`, and a failed list or lookup gives that manager an `Error` and no decisions. It
  creates the `UpdateSources` only when there is a manager to check and disposes them before it returns.
  `PackageUpdatesReport.HasFailure` and `HasSkipped` tell a complete answer from a partial one. Error texts can carry
  tool or registry output; escape them before printing.
- `UpdateSources` bundles the advisory, release-date, and latest-version sources with the clock.
  `UpdateSources.CreateDefault()` builds `OsvAdvisorySource` and `RegistryReleaseSource` over one HTTP client from
  `PackageHttp.CreateHandler()` and contacts nothing until asked.
- `PackageName` and `PackageVersion` are value objects. `Create` returns a `Result` with a validation error that never
  repeats the input. Names and versions are kept exactly as written; versions only compare for equality, because
  ordering is specific to each package manager.
- `PackagePin.Create` takes a `PackageVersion` and an artifact checksum with a `sha256:` or `sha512:` prefix and a
  hexadecimal digest. `PackageRequirement.Pinned` carries the pin into proposed changes for programmatic callers.
  Installed-state commands expose versions. A matching version needs no version change and leaves artifact integrity
  unverified.
- `PackageSet.Create` snapshots requirements, checks that each package appears once, and orders them by name.
  `DeclaredPackages.SetFor` returns that immutable set for a manager. The declaration also checks ownership across
  managers.
- `IPackageReader` is the read contract. `AptPackageReader` (`dpkg-query`), `CargoPackageReader` (`cargo install --list`),
  `NpmPackageReader` (`npm ls --global --json`), `FlatpakPackageReader` (`flatpak list --app`), `SnapPackageReader` (`snap list`), and
  `BrewPackageReader` (`brew list --versions`) run their tool through
  `IProcessRunner` from `PandaBear.SharedKernel.Processes`, from an absolute path, without a shell, and in the `C` locale. A tool that cannot run, runs too long, exits non-zero, or prints a line that cannot be read is an error result whose message
  carries the first line of the tool's error output, never an empty list. Cancellation still throws. A package listed twice is an
  error. apt reports held packages as installed and a partly installed package as an error to repair. A
  Flatpak application without a version text is identified by its branch. The snap reader checks and skips the table
  header and lists base snaps such as `core24`. The brew reader treats a formula with more than one installed version as an
  error (run `brew cleanup`); empty brew output is an empty list.
- Hosts pass `BoundedProcessRunner` from the Processes package: a two-minute limit and a limit of 8 Mi characters on each
  of the standard output and the standard error it keeps. Output above the limit ends the process; a process that leaves its
  output open after it exits is an error, and cancellation kills it. Tests use a fake `IProcessRunner` and, where the tools
  exist, real processes.
- `PackagePlanner.Plan` returns the requirements that the installed packages do not meet, ordered by name: a missing
  package is an `Install`, a different installed version than an exact required one is a `ChangeVersion`. Extra
  installed packages are outside the declaration's scope.

- The manager-specific `UpdatePolicy.Decide` overload blocks affected candidates and holds a candidate until its version
  is known to be newer than the installed version and any review pin. Cargo and npm use SemVer precedence and ignore build
  metadata. A documented fix for the affected installed interval waives the release age. Other eligible candidates must
  meet `MinimumReleaseAge`. The original overload evaluates advisories and age for candidates that callers already
  ordered. Every result includes its reason and uses the caller's evaluation time.
- `AdvisoryVersionRange` records a SemVer interval from its introduction, inclusive, to its explicit fix, exclusive.
  Either bound can be absent. An advisory can also list exact affected versions. Reviewing a pin uses its version as
  a lower bound for the proposed change. Inventory continues to skip packages with exact declared pins.

- `IAdvisorySource`, `IReleaseDateSource` and `ILatestVersionSource` supply the policy's inputs. A failed, refused,
  oversized, or partial lookup is a failed `Result`, never an empty list, and every call takes a `NetworkAccess` that is
  `Denied` by default. `OsvAdvisorySource` (osv.dev) and `RegistryReleaseSource` (crates.io and npm) implement them over an
  injected `HttpClient` (build its handler with `PackageHttp.CreateHandler()`): HTTPS only, a time limit, a response size
  limit, no redirects, and an in-memory cache with expiry that never stores a failure. OSV matches the exact versions
  that are asked about and contributes supported SemVer introductions and fixes from the matching package record.
  Other range formats still use exact query membership. apt needs an OSV release such as `Debian:12`, and Flatpak is
  unsupported. `UpdatePlanner` finds candidates (a package the registry does not know is
  listed in `UpdateSearch.NotOnRegistry` and not checked), queries both versions of each, and returns decisions only when
  every other lookup succeeded. See the
  [advisory ADR](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/adr/20261109-package-advisories.md).

The package manager readers and the OSV and registry sources stay in this package rather than in
`SharedKernel.Packages.<Provider>` libraries: the managers are a closed `PackageManagerKind` set that the configuration
format names, so a new manager changes this package either way, and none of them adds a package dependency a consumer
could avoid.

## Convergence

`PackageConvergence.Run` previews current differences by default. A host can explicitly enable application and supply
`PackageApplyPorts` with executors and a privilege policy.

```csharp
var convergence = new PackageConvergence(inventory);
var preview = await convergence.Run(configuration.Packages, cancellationToken: ct);
var result = await convergence.Run(
    configuration.Packages, apply: true, ports: hostApplyPorts, cancellationToken: ct);
```

Each run starts with a fresh observation. Application requires a complete initial plan and follows package manager order,
then ordinal package-name order. An elevated step requires a dedicated executor and approval for its manager and exact
change. The report records the selected privilege and outcome for each change. A failed, unsupported, or denied change
leaves later independent changes eligible to run.

After application, the report includes the plan from a fresh installed-state check. The run is complete when every step
succeeds and that plan is empty. Retrying replans against the state that actually exists. An executor must also recheck
state at the execution boundary so a change already satisfied at that point is safe to repeat. For an installation with a
checksum pin, the executor must verify the artifact before installing it. Installed-state readers verify version convergence.

The CLI offers `plan` and `updates`. Manager-specific execution and elevation remain with the host supplying the ports.

## Limits to know

- An empty list from a successful tool is a real answer ("nothing installed") but also what a tool prints when it reads the
  wrong home directory or prefix, as a service account might. Do not act on an empty list without a check that fits.
- apt names use `dpkg-query`'s `${binary:Package}`, which adds the architecture (`libc6:amd64`) when a bare name would be
  ambiguous. Requirements for such packages must use that qualified name.
- `UpdatePolicy` treats an empty advisory list as "no known advisories". A caller whose advisory feed failed must fail
  before it calls the policy; `UpdatePlanner` does that.
- OSV names Debian and Ubuntu packages by source package, so an installed apt binary package with another name matches
  nothing and looks clean. Map binary names to source names before an apt lookup.
- A registry's selected version can be older or have the same precedence as the installed version. The planner reports
  its policy decision for a different version and holds it when precedence does not establish an upgrade.
- `npm ls` exits non-zero when the global tree has problems such as missing peers, even though it still prints a list; the
  reader reports that as an error.
