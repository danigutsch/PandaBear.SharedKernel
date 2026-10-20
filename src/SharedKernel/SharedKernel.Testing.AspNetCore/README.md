# SharedKernel.Testing.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

ASP.NET Core web application factory helpers for SharedKernel tests.

## Purpose

This package centralizes small `WebApplicationFactory<TEntryPoint>` setup helpers that are reusable
across ASP.NET Core test projects.

## Scope

- Create test hosts for ASP.NET Core entry point assemblies
- Optionally set the hosting environment
- Optionally apply test service overrides

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.AspNetCore`. C# namespace: `PandaBear.Testing.AspNetCore`. Assembly/project identity: `SharedKernel.Testing.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
