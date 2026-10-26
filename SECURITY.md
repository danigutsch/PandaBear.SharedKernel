# Security

Source is public at [danigutsch/PandaBear.SharedKernel](https://github.com/danigutsch/PandaBear.SharedKernel).

## Supported versions

All packages are previews. Only the latest preview candidate receives security fixes; see the [release policy](docs/release-policy.md). This section becomes a version table at the first stable release.

## Reporting a vulnerability

Report a suspected vulnerability privately through GitHub private vulnerability reporting: open the repository's [Security tab](https://github.com/danigutsch/PandaBear.SharedKernel/security) and choose "Report a vulnerability". Do not put credentials, customer data, or exploit details in public issues or pull requests.

A vulnerability is a flaw in a package or tool of this repository that lets an attacker break confidentiality, integrity, or availability, or that leaks credentials or personal data through telemetry or diagnostics. Reports are handled under coordinated disclosure: the repository owner confirms the report, prepares a fix in a private channel, and publishes the fix and its description together. No fixed response or fix times are promised.

## Security practice

Keep vulnerability auditing and warnings-as-errors enabled. Provider implementations must preserve parameterization, cancellation, bounded resource use, and meaningful failure reporting. Telemetry must avoid credentials and personal data. Treat package/analyzer/tool dependencies as executable supply-chain inputs and review transitive changes. Registry publication and application adoption require separate review.
