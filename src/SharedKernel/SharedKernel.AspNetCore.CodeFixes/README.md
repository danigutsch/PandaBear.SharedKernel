# SharedKernel ASP.NET Core code fixes

Use `PandaBear.SharedKernel.AspNetCore.CodeFixes` with the
[ASP.NET Core analyzer](../SharedKernel.AspNetCore.Analyzers/README.md). For reviewed runtime
binding, the fix adds the missing annotations to the mapping method. Prefer a source-generated
request delegate when the route must support trimming or Native AOT.
