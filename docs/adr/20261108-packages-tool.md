# Packages tool

Date: 2026-11-08. Status: accepted.

A machine's packages come from several package managers, and the same program is often available from more than one of
them (`ripgrep` from apt and from cargo). Two managers that both own a package leave the machine in a state that
neither can explain. `sharedkernel-packages plan` compares what a repository declares with what is installed and refuses
a declaration in which a package has more than one owner. It is the first step of a convergence tool: it reads and
reports, and it changes nothing.

## Decision

- The tool lives in SharedKernel and is generic. The consuming repository supplies the package lists in
  `.config/sharedkernel-packages.json`, strict JSON with `"version": 1`, as the other tools do. No package name, owner,
  or host is embedded in the tool.
- The reusable parts are in `SharedKernel.Packages`: validated package names and versions, a reader for each manager
  behind `IPackageReader`, `PackagePlanner`, and `DeclaredPackages`. The tool only reads the file, finds the programs on
  `PATH`, and prints.
- **One owner per package** is a property of `DeclaredPackages`, not a check in the tool: it cannot be built with a name
  under two managers or twice under one, so every caller gets the rule. The owner is the name; a version pin does not make
  two declarations different. Names are compared as written, so `bat` for apt and `bat` for cargo overlap, while differently named packages that happen to provide the same program do not; the declaration has to name a program the same way everywhere.
- `plan` checks the declaration and finds every tool before it runs any tool. Exit code 0 means everything declared is
  installed, 1 means a package is missing or at another version, and 2 means the result cannot be known (an invalid
  configuration, a package with two owners, or a tool that is missing or cannot be read). A caller can therefore tell
  drift from a broken run without reading the standard error.
- It uses the installed state only. Updating, release ages, and advisories are separate work that builds on the update
  policy in the same library and needs a source of release dates.

## Deferred

- Applying changes (`apply`, behind `--force`), because it needs privilege handling and a decision per manager.
- Reporting a declared package that is also installed by another manager (an undeclared second owner).

## Addendum: snap and Homebrew readers

Date: 2026-11-09. Snap and Homebrew are now readable (`snap`, `brew`); applying changes is still deferred.

- **snap** reads `snap list`, a table with the columns `Name Version Rev Tracking Publisher Notes`. The header line is
  checked and then skipped on purpose; a different header is an error, so a changed table format never reads as data.
  Base and core snaps (`core24`, `snapd`) are installed snaps and are listed. A data line that does not have exactly six
  columns is an error, never skipped. Versions such as `2025.3` or `1.0-beta` are kept as text. No output at all is an
  empty list, because `snap` prints its "no snaps" notice on the standard error. The real output was checked against
  `snap` 2.77 on a machine.
- **brew** reads `brew list --versions`, one `name version` line per formula or cask. Homebrew keeps old versions
  installed until `brew cleanup`, and then prints `name v1 v2`. The reader reports an **error** for several versions
  instead of picking the highest: Homebrew orders versions by its own rules that the library does not implement, the
  newest may not be the linked one, and a wrong pick would hide drift in a tool whose job is to tell the truth. This is
  the same rule as a package listed twice. The cost is that the error must be fixed with `brew cleanup` before `plan`
  runs. Formula names with `@`, such as `openssl@3`, are valid. A tap-qualified name such as `user/tap/foo` is **rejected in the declaration** (`DeclaredPackages`): `brew list --versions` prints only the short name, so such a declaration would be reported missing forever. A formula and a cask that share a name make brew list the name twice, which is reported as an error that says so.
- **Empty-list caveat:** `brew` prints nothing and exits 0 when nothing is installed, so empty output is an empty list.
  A broken `brew` that prints nothing and exits 0 cannot be told apart; a non-zero exit is always an error. The brew
  format is taken from Homebrew's documentation, because `brew` was not available to test against.
- The `PackageManagerKind` values `Snap` (4) and `Brew` (5) follow the existing ones, so their numeric values do not change.
- **Known gap:** on Ubuntu, `firefox` and `thunderbird` are transitional apt packages that install the snap. An apt
  declaration of them counts as met while the snap is the real owner, and the one-owner rule cannot see it because the
  two names are declared under different managers by the user. Declare such packages under `snap` only.
- The child process now also gets `LC_MESSAGES=C` and `LANG=C` and no `LANGUAGE`, because `LANGUAGE` can override the
  message language and the snap header is checked as English text.
