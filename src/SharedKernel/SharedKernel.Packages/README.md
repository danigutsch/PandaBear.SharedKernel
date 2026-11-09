# PandaBear.SharedKernel.Packages

A small model of the packages on a machine: validated names and versions, a reader for installed packages, and a
read-only comparison with what should be installed. Targets .NET 10. It installs, removes, and updates nothing.

```csharp
using PandaBear.Packages;

var reader = new AptPackageReader(new SystemProcessRunner(), "/usr/bin/dpkg-query");
var installed = (await reader.ListInstalled(ct)).Value;

PackageRequirement[] wanted = [new(PackageName.Create("git").Value), new(PackageName.Create("ripgrep").Value, PackageVersion.Create("14.1.0-1").Value)];
foreach (var change in PackagePlanner.Plan(wanted, installed))
{
    Console.WriteLine($"{change.Kind}: {change.Requirement.Name}");
}
```

- `PackageName` and `PackageVersion` are value objects. `Create` returns a `Result` with a validation error that never
  repeats the input. Names and versions are kept exactly as written; versions only compare for equality, because
  ordering is specific to each package manager.
- `IPackageReader` is the read contract. `AptPackageReader` (`dpkg-query`), `CargoPackageReader` (`cargo install --list`),
  `NpmPackageReader` (`npm ls --global --json`), `FlatpakPackageReader` (`flatpak list --app`), `SnapPackageReader` (`snap list`), and
  `BrewPackageReader` (`brew list --versions`) run their tool through
  `IProcessRunner` from an absolute path. A tool that cannot run, runs too long, exits non-zero, or prints a line that cannot be read is an error result whose message
  carries the first line of the tool's error output, never an empty list. Cancellation still throws. A package listed twice is an
  error. apt reports held packages as installed and a partly installed package as an error to repair. A
  Flatpak application without a version text is identified by its branch. The snap reader checks and skips the table
  header and lists base snaps such as `core24`. The brew reader treats a formula with more than one installed version as an
  error (run `brew cleanup`); empty brew output is an empty list.
- `SystemProcessRunner` starts a real process without a shell, in the `C` locale, with a two-minute limit and a limit of 8 Mi
  characters on each of the standard output and the standard error it keeps. Output above the limit ends the process; a process that leaves its
  output open after it exits is an error, and cancellation kills it. Tests use a fake `IProcessRunner` and, where the tools
  exist, real processes.
- `PackagePlanner.Plan` returns the requirements that the installed packages do not meet, ordered by name: a missing
  package is an `Install`, a different installed version than an exact required one is a `ChangeVersion`. Extra
  installed packages are not differences. Planning twice after applying the changes yields an empty plan.

- `UpdatePolicy.Decide` judges an `UpdateCandidate` (installed version, newer version, release time) against known
  `Advisory` values and a `MinimumReleaseAge`: a candidate that an advisory affects is `Block`ed; otherwise an installed
  version that an advisory affects makes the update `Urgent`, whatever the candidate's age; otherwise the candidate is
  `Allow`ed once it has been public for the minimum age and `Hold`s until then. Every decision carries its reason. Time is
  passed in, so decisions are deterministic. An advisory lists its exact affected versions, because this library never
  compares versions.

Snap and Homebrew readers, release-date and advisory sources, and applying changes are separate packages and issues; see the
[package management tooling epic](https://github.com/danigutsch/PandaBear.SharedKernel/issues/344).

## Limits to know

- An empty list from a successful tool is a real answer ("nothing installed") but also what a tool prints when it reads the
  wrong home directory or prefix, as a service account might. Do not act on an empty list without a check that fits.
- apt names use `dpkg-query`'s `${binary:Package}`, which adds the architecture (`libc6:amd64`) when a bare name would be
  ambiguous. Requirements for such packages must use that qualified name.
- `UpdatePolicy` treats an empty advisory list as "no known advisories". A caller whose advisory feed failed must fail
  before it calls the policy.
- `npm ls` exits non-zero when the global tree has problems such as missing peers, even though it still prints a list; the
  reader reports that as an error.
