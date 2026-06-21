# SharedKernel.Results.AspNet

ASP.NET Core adapters for `SharedKernel.Results`.

## Purpose

This package keeps HTTP-specific result behavior outside the transport-neutral
`SharedKernel.Results` package.

It currently provides:

- `ResultStatusHttpExtensions.ToHttpStatusCode()` for converting result statuses to conventional
  ASP.NET Core HTTP status codes.

## Package Conventions

- `PackageId`: `SharedKernel.Results.AspNet`
- `RootNamespace`: `SharedKernel.Results.AspNet`
- `IsAotCompatible=true`
- package README included during packing
