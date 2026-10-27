# Blazor helpers package

Date: 2026-10-27. Status: accepted.

Blazor applications repeat three patterns: mapping a failed HTTP outcome to user copy and showing server validation errors, reporting handled UI failures, and showing accessible upload status. The helpers live in a new package, `PandaBear.SharedKernel.Blazor` (namespace `PandaBear.Blazor`), rather than in `SharedKernel.HttpClients`.

## Why a new package

`SharedKernel.HttpClients` is host-agnostic: it references no UI framework. The helpers need `Microsoft.AspNetCore.Components` (`EditContext`, `ComponentBase`, `ErrorBoundaryBase`), so adding them to HttpClients would force the ASP.NET Core shared framework on every HTTP client consumer, including services and tools that have no UI.

## Cost

- One more package and one more preview surface to maintain, with a public API baseline.
- No new NuGet dependency. The project uses `FrameworkReference Microsoft.AspNetCore.App`, as the other ASP.NET Core packages do, and a project reference to `SharedKernel.HttpClients`, so consumers receive that package's existing dependency tree (resilience, service discovery, OpenTelemetry). It cannot be avoided because the helpers consume `HttpOutcome` and `UploadOperationState`.
- The components are plain C# classes that build their render tree. The repository has no Razor class library precedent, and the Razor SDK would add a compiler step, static web asset handling, and generated code to the analyzer surface without adding behavior. A component can move to `.razor` later without changing its public API.
- The test project adds the already centrally pinned `bunit` package, which brings AngleSharp, AngleSharp.Css, AngleSharp.Diffing, and `Microsoft.AspNetCore.Components.WebAssembly` as test-only transitive dependencies. No shipped package references them.

## Rules the helpers enforce

- Server-authored validation text is never joined into a free-form message (RFC 9457 leaves `detail` to the server; clients do not parse it). Field-keyed errors go to a `ValidationMessageStore`; everything else shows caller-supplied fixed copy.
- Field names and message lengths are bounded; blank, multi-line, or stack-trace-like messages are replaced by a caller-supplied field message.
- Handled UI failures log only the exception type name, an operation name chosen by code, and the trace id, under a stable `EventId`. The counter is an abstraction, `IHandledFailureCounter`, so the package works with or without `SharedKernel.AspNetCore`.
- Components follow the GOV.UK error summary pattern and standard live-region, progress, and focus semantics.

Registry publication, releases, and consumer adoption remain separate actions.
