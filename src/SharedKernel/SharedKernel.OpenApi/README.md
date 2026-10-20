# SharedKernel.OpenApi

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable OpenAPI helpers for API projects in this repository.

Current scope:

- define boundary-specific OpenAPI document registrations

Non-goals:

- generic API-project abstractions unrelated to OpenAPI
- application-specific route-group extension methods

Usage:

- keep reusable OpenAPI registration and generation helpers here
- keep application-specific document lists close to the consuming API project

## Package identity and availability

Package ID: `PandaBear.SharedKernel.OpenApi`. C# namespace: `PandaBear.OpenApi`. Assembly/project identity: `SharedKernel.OpenApi`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
