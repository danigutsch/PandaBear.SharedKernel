# Package advisories and release dates

Date: 2026-11-09. Status: accepted.

`UpdatePolicy` judges an update from advisories and a release time, and its README says that an empty advisory list
means "no known advisories". Nothing supplied those inputs, and a caller whose feed failed could pass an empty list and
get `Allow`. This decision adds the sources and makes a failed lookup impossible to mistake for an empty one.

## Decision

- **Ports.** `IAdvisorySource`, `IReleaseDateSource` and `ILatestVersionSource` return `Result`. A failed, refused, oversized,
  unreadable or partial lookup is a failure, never an empty success. `UpdatePlanner` returns the first source failure and no
  decisions at all, so the policy never runs on partial data.
- **Opt-in per call.** Every port method takes a `NetworkAccess`, which is `Denied` by default; the adapters and the planner
  answer `Forbidden` without any request when it is not `Allowed`. The tool's `updates` command sets it only from
  `--allow-network`, and without the flag it refuses before it reads the configuration or starts a tool. The tool makes no
  outbound call by default.
- **OSV adapter.** `OsvAdvisorySource` asks `POST /v1/querybatch` which vulnerabilities affect each exact package version,
  then reads each one from `GET /v1/vulns/{id}` for its severity. Batches hold 100 versions; `next_page_token` is followed for
  at most 20 pages; at most 1000 distinct vulnerabilities are read per call. A result count that differs from the query count,
  a missing or unsafe identifier, any non-200 answer, or a record for another identifier fails the call.
- **Range-only records.** `Advisory` lists exact versions and the library never compares versions. Real OSV records
  mostly give only ranges (checked on 2026-11-09: GHSA-p6mc-m468-83gw for lodash has `SEMVER` ranges and no `versions`
  list). Comparing ranges would need each ecosystem's version order, so the adapter does not. It asks OSV about the exact
  versions that matter (the installed and the candidate version) and OSV, which knows each ecosystem's order, answers
  which of them are affected. An advisory therefore lists the versions that were asked about and reported as affected,
  plus any `affected[].versions` the record lists (at most 1000). A range-only record is exact for every version asked about
  and never claims a version that was not checked. The cost is that `Advisory.Affects` is only meaningful for versions
  that were passed to the query; the planner passes both.
- **Severity.** In order: `database_specific.severity` (`CRITICAL`, `HIGH`, `MODERATE` or `MEDIUM`, `LOW`), then the computed
  base score of a `CVSS_V3` vector (bands 9.0, 7.0, 4.0). CVSS v2, CVSS v4, Ubuntu priorities and anything unreadable give
  `High`: not knowing never understates an advisory. Severity does not change the policy's decision today.
  Withdrawn records are left out.
- **Ecosystems.** cargo is `crates.io`, npm is `npm`, apt is the release the caller passes (`Debian:12`, `Ubuntu:24.04:LTS`,
  validated). Flatpak and any other manager fail with `Invalid`. OSV names Debian and Ubuntu packages by source package, so
  an installed binary package whose name differs (`libc6`, source `glibc`) matches nothing and looks clean; apt is therefore
  not used by the tool and a library caller must map binary to source names itself.
- **Cache.** In memory, per source object, one hour by default, with `TimeProvider` expiry and at most 10 000 entries. Only
  answers are stored (an empty advisory list is a real answer; an error never is). A file cache was rejected: the tool is a
  one-shot process, so a file would add a writable trust surface and stale advisories for no gain, and a long-lived caller
  gets the in-memory cache.
- **Release dates and newest versions.** `RegistryReleaseSource` reads crates.io (`/api/v1/crates/<name>` for
  `max_stable_version`, `/api/v1/crates/<name>/<version>` for `created_at`) and the npm registry (`/<name>`, `dist-tags.latest`
  and the `time` map). apt and Flatpak have no cheap authoritative source and fail with `Invalid`. Without version order, a
  newest stable version that differs from the installed one is reported as a candidate even if it is older.
- **Transport.** All requests go through an injected `HttpClient`; tests use fake handlers and never touch the network. Each
  request is HTTPS-only to a fixed host, has a timeout (30 seconds), and a response cap (8 MiB OSV, 32 MiB registries); a
  redirect, an answer from another host, or an oversized body is an error. `PackageHttp.CreateHandler` disables automatic
  redirects and cookies. Names and versions are URL-escaped per path segment. The `User-Agent` is the generic
  `sharedkernel-packages/1.0 (+https://github.com/danigutsch/PandaBear.SharedKernel)`. Error messages never carry response
  bodies or request addresses, and nothing secret is sent.
- **Packages the registry does not know.** `UpdatePlanner.FindUpdates` returns an `UpdateSearch`: a `NotFound` answer for one
  package (a git or path crate, a linked npm package, no stable version) lands in `NotOnRegistry` and the other packages are
  still searched, so one unknown package does not hide advisories for the rest. It is a per-package "not checked", never an
  allow, and the tool exits 2 whenever anything was not checked (such a package or an unsupported manager). Any other failure
  (network, 5xx, malformed answer, a body that breaks off or is corrupt) still fails the whole call.
- **Tool scope.** `sharedkernel-packages updates` supports cargo and npm. Other declared managers are reported as
  unsupported and skipped. `minimumReleaseAgeDays` is an optional, strict, additive field of version 1, so existing files
  stay valid; the age must come from the flag or the file, with no built-in default.

## Deferred

- Snap, Homebrew and apt release dates.
- A persistent cache.
- Version-aware range evaluation.
- Spacing of crates.io requests (its crawler policy asks for one a second); the tool looks up a few crates sequentially.
