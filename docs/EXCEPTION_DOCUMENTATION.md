# Exception documentation

Document stable caller-visible failures on public APIs with XML `<exception cref="...">` tags. Describe the triggering condition and parameter, rather than the implementation line that throws. Deliberately preserved dependency exceptions can be part of a contract even without a direct throw.

- Document null, empty, and range guards that the boundary actually rejects; keep tags consistent with nullable annotations and accepted values.
- Describe expected validation failures through the returned Result or HTTP outcome in `<returns>` or `<remarks>`. Do not invent a validation exception.
- For cancellable asynchronous operations, document `OperationCanceledException` and the supplied cancellation-token condition. Do not promise the incidental subtype `TaskCanceledException`.
- Document `NotSupportedException` only for an intentionally unsupported operation or mode. A helper's implementation must not impose an undocumented stream-seeking requirement on an abstraction that accepts non-seekable streams.
- Keep provider exception contracts in the adapter that intentionally exposes them. Provider-neutral contracts must not acquire EF Core or Npgsql exception dependencies through their documentation.
- Do not enumerate incidental allocation, socket, or downstream exceptions as guaranteed contracts. Internal helpers need documentation when another component relies on a non-obvious failure contract.

State stream/resource ownership and when transfer occurs. Cancellation does not promise rollback of a commit or reversal of external effects unless the implementation guarantees it. HTTP Problem Details are response contracts, not XML exception contracts. Never include credentials, connection strings, private payloads, or personal data in examples or output.

Review documentation against actual guards, translated outcomes, cancellation propagation, and relevant tests when changing the touched API. Use `<inheritdoc/>` when an implementation preserves its interface contract; describe additional concrete adapter behavior only when intentional. Preserve the [Result decision](adr/20251108-result-pattern-over-exceptions.md) and current module contracts.

The compiler checks XML structure/references and configured missing-public-documentation diagnostics. These checks do not prove exception-contract completeness. Preserve scoped analyzer policy; no new analyzer, dependency, global diagnostic severity, or exception behavior is introduced by this guidance.

Reference: [Microsoft recommended XML documentation tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags).

Reviewed 2026-10-20. Library maintainers own this guidance and its enforcement boundaries.
