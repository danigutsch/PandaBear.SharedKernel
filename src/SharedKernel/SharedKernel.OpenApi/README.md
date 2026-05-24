# SharedKernel.OpenApi

Reusable OpenAPI helpers for API projects in this repository.

Current scope:

- define boundary-specific OpenAPI document registrations
- detect ASP.NET Core build-time OpenAPI document generation

Non-goals:

- generic API-project abstractions unrelated to OpenAPI
- application-specific boundary definitions

Usage:

- keep reusable OpenAPI registration and generation helpers here
- keep application-specific document lists close to the consuming API project
