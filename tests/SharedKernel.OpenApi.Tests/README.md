# SharedKernel.OpenApi.Tests

Tests for reusable OpenAPI helpers in `src/SharedKernel/SharedKernel.OpenApi`.

This project uses a lightweight in-process ASP.NET Core host and
`IOpenApiDocumentProvider` to exercise the real OpenAPI pipeline without adding
browser, TCP, or full application-hosting overhead.

Current focus:

- multipart form request-body normalization
- generic SharedKernel transformer behavior
