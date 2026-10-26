# Diagnostic and warning policy

Classify findings by demonstrated impact and confidence. An actionable security, correctness, or reliability failure blocks acceptance. Cosmetic advice is not automatically a blocker; incomplete analysis is not evidence of success. Actual enforcement comes from `Directory.Build.props`, `.editorconfig`, tools, and `.github/workflows/ci.yml`, not from this classification guide.

| Signal | Current standalone boundary |
| --- | --- |
| Compiler/analyzers | Build errors and unsuppressed warnings under warnings-as-errors block builds. Suggestions remain advice; scoped suppressions remain reviewed debt. |
| NuGet restore/audit | Failed restores, inconsistent locked graphs, and diagnostics promoted by current configuration block validation. Auditing is enabled for all dependencies at the high threshold; review actionable vulnerabilities and transitive impact explicitly. |
| Formatting/generated docs | Nonzero required formatting or generated-diagram checks fail repository quality validation. Preserve drift failures rather than accepting generated output without review. |
| Tests | Failed tests, host crashes, missing expected execution, and corrupt results require resolution. Repeated reruns do not establish flaky-test acceptance. |
| Package/API/platform evidence | Missing required API compatibility, package-consumption, hash, or platform evidence prevents the corresponding candidate claim. Keep unavailable evidence pending. |
| Coverage | Existing local `coverlet.MTP` collection supplies evidence only for instrumented runs. There is no configured coverage percentage gate or hosted coverage service here. |

The standalone repository has no current Dependency Review or Secret Scan workflow. Do not describe application-owned checks, changed-code coverage thresholds, or hosted metrics as library enforcement. Review dependencies, secrets, and known unsafe behavior regardless of which automated services exist; never classify confirmed secrets or missing security analysis as ordinary cosmetic debt.

Do not hide required failures using ignored exit codes, `continue-on-error`, broad catch wrappers, or unconditional successful aggregation. Clearly distinguish passed, failed, and not-run checks. Advisory work must be identified explicitly and must not contain blocker work.

## Scoped exceptions

Keep accepted exceptions beside their owning configuration or in a linked durable record. Record the exact producer/rule, file/project scope, baseline quantity when countable, rationale and evidence, responsible maintainer, remediation, and expiry or review date. Explain how validation catches findings outside the exception. Prefer rule-and-path scope over globally disabling a category.

A baseline is a reviewed fixed set or count, not permission for growth. Do not regenerate it silently to absorb new failures; ratchet it down as fixes land. Legacy suppressions lacking ownership or review dates remain debt to assess when their rule is touched. This document does not claim the current suppressions already meet every record requirement.

Review exceptions at expiry and when the owning SDK, analyzer, package, or generator changes. For new findings, identify the producer and rule, reproduce safely, classify impact/confidence, and fix or record a narrow reviewed exception. Keep evidence sanitized and preserve the original check's exit status. This guidance changes no severity, required check, dependency, or runtime behavior.

Reviewed 2026-10-20. Library maintainers own this guidance and its enforcement boundaries.
