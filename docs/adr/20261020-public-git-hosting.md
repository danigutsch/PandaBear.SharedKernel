# Public Git hosting

Date: 2026-10-20. Status: accepted.

The user explicitly authorized public source publication to `https://github.com/danigutsch/PandaBear.SharedKernel`. This supersedes the earlier handoff restriction requiring a private Git repository. Preserve existing extraction provenance and local history; publish only reviewed source without credentials, private captures, local feeds, or customer data.

Public Git hosting does not authorize NuGet or another package registry publication, stable tags, or automatic application adoption. Those remain separate actions. The subsequent user instruction keeps all packages in preview (prerelease) for now. CI produces prerelease candidates only, with no stable-preparation dispatch input. Stable packages, tags, and releases require separate authorization. The reusable version calculator and CLI APIs remain unchanged.

Repository and package READMEs use the real repository-wide GitHub Actions build badge and canonical source-document URLs. Coverage and registry-publication badges remain absent until genuine reporting/publication sources exist. A repository build badge does not claim package publication or package-specific coverage.
