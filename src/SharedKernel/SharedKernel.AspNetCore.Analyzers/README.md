# SharedKernel ASP.NET Core analyzers

Reference `PandaBear.SharedKernel.AspNetCore.Analyzers` privately in projects that register Minimal API routes.
Use the Request Delegate Generator for routes intended for trimming or Native AOT.

`SKASPNET001` reports named endpoint mapping methods that call an annotated framework overload through runtime
binding and lack the corresponding trimming or dynamic-code annotations. It resolves the effective method after
interceptors, so source-generated route bindings use their own annotation contract.

Prefer a route handler shape that RDG can generate. Put reviewed runtime fallbacks in named mapping methods.
Use the companion code fix to add the missing `RequiresDynamicCode` or `RequiresUnreferencedCode` attributes.
These attributes document the caller’s compatibility obligation.
