# SharedKernel.Testing.AspNetCore

ASP.NET Core web application factory helpers for SharedKernel tests.

## Purpose

This package centralizes small `WebApplicationFactory<TEntryPoint>` setup helpers that are reusable
across ASP.NET Core test projects.

## Scope

- Create test hosts for ASP.NET Core entry point assemblies
- Optionally set the hosting environment
- Optionally apply test service overrides
