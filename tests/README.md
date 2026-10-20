# Library tests

Use the repository-pinned SDK and Microsoft.Testing.Platform runner. After the documented locked restore and Release build, run `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`. Run a single project with `dotnet test --project <path-to-csproj> -c Release --no-build --no-restore`; narrow local debugging with `--filter-class`, `--filter-method`, `--filter-namespace`, or `--filter-trait`. CI selects complete projects and nightly runs cover the full solution.

Provider integration tests require a working local container runtime. Preserve meaningful cancellation, persistence, cleanup, privacy, and boundary regressions. Do not cap test-module concurrency or mask cleanup errors. Analyzer/generator tests exercise compilations and emitted contracts; package consumers validate installed immutable artifacts separately from project-reference tests.

Coverage uses the existing `coverlet.MTP` extension; collection and its limits are documented in the root README. No hosted coverage percentage or service is implied. The two explicitly packaged test-support projects have their own package README files; ordinary test projects remain non-packable.
