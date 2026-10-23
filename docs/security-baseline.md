# SharedKernel security baseline

## Ownership

SharedKernel owns reusable security primitives and repository tooling. Consumers own origins,
rate-limit policies, authorization rules, deployment configuration, and application threat models.
Document checks establish documentation contracts only; source-text matches do not prove that
runtime controls work. Runtime security is covered by the maintained library and application tests.

## Automated checks

Run from the library root:

```text
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- security baseline --root .
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- security scan --root . --output TestResults/security
```

Each repository owns `.config/security-baseline.json`: version `1`, with a nonempty `documents`
array whose entries contain a repository-relative Markdown `path` and a `requiredText` string array.
Paths may not traverse outside the root or include symbolic links. Empty `requiredText` arrays
require document existence only. The checker never prints document contents or missing text values.

Scans additionally require Gitleaks on an absolute PATH entry. The shared setup action installs
the reviewed binary on Linux X64. Gitleaks scans current files, with repository-owned rules in
`.gitleaks.toml`; ambient Gitleaks configuration variables are removed. This command does not scan
Git history. Runtime tests and full locked restore retain their own CI jobs. NuGet Audit remains
enabled for direct and transitive dependencies at the repository's configured severity threshold.
Codex Security deep scans remain operator-invoked and are not a CI dependency.

The security workflow runs CLI contract tests on Linux and Windows. Its Linux scanner job also
explicitly runs `InstalledSecurityScannerTests` with `--explicit only`: that test requires the
verified scanner binary, detects a generated synthetic token, checks console/SARIF redaction, and
then checks clean source. Ordinary library test runs leave this external-tool test explicit.

## Failure handling

Command-line parse errors return `1`, matching the other repository commands. Security runtime
exit codes are `0` for success, `1` for baseline violations or secret findings, `2` for invalid or
unreadable baseline configuration/paths, and `3` for scanner failures, cancellation, or invalid
reports. Missing tools never count as successful scans. Scanner execution has a ten-minute limit.
Raw scanner stdout and stderr are not forwarded; bounded outcome diagnostics identify failures.

The scan writes `gitleaks.sarif` in the output directory only after validating scanner success or
findings and the SARIF result count. Existing reports are never overwritten: use a fresh directory
or retain and move prior evidence deliberately. Upload jobs have separate permissions; fork scans
must not gain write tokens. Generated reports must remain outside tracked source.
Symbolic links are rejected in report-directory paths to prevent writes through redirected directories.

## Runtime boundaries

Roadmap initialization, configuration updates, and issue mapping persistence reject symbolic links
and junctions in the checkout root, its ancestors, and write destinations. Changes use exclusive,
random staging files and expected-content checks. Operators must retain control of directory entries
throughout a modifying command. A remote issue created before a local persistence failure is reported
with its issue number for recovery; the creation lock remains in place.

Parent and blocker cycle checks use iterative, linear graph traversal. Shared acyclic dependencies
are processed once per traversal, deep chains do not consume recursive stack space, and every cycle
member remains diagnostic.

Image text generation enforces the finite `MaxImageBytes` budget before base64 encoding or HTTP
submission. The default is 5 MiB; hosts can configure their own positive limit and retain ownership
of the input stream. Successful branding-save responses are limited to the smaller of 1 MiB and
`HttpClient.MaxResponseContentBufferSize`, counting actual streamed bytes before JSON parsing.
Both boundaries preserve caller cancellation and existing body-timeout behavior. See the
[AI contract](../src/SharedKernel/SharedKernel.AI/README.md) and
[branding contract](../src/SharedKernel/SharedKernel.Branding/README.md).

## Supply chain

Gitleaks `8.30.1` replaces the application's older `8.9.0` scanner. This executable upgrade is a
supply-chain change; no new NuGet dependency is introduced. Its Linux X64 archive SHA-256 is
committed beside the shared installer, checked against the upstream release assets on 2026-10-21.
Review scanner updates and new transitive dependencies explicitly.

Consumers use immutable, hashed SharedKernel candidates. Keep application policy and compatibility
evidence in the consuming repository and retain previous packages for rollback. Registry publication
and external GitHub changes remain separate from local preparation.

References: [Gitleaks 8.30.1](https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1),
[Gitleaks usage](https://github.com/gitleaks/gitleaks), and
[NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).
