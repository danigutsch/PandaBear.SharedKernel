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
- Snap and Homebrew.
