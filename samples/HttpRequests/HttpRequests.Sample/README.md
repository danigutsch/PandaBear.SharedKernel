# HTTP request primitives sample

Exercises `GetSubjectId`, validated idempotency keys, and explicit retry with an
unchanged idempotency key. It also composes the real HTTP client defaults with the resilience
customization callback and resolved-client-IP forwarding. A pooled named client sends two synthetic
visitor contexts through an in-memory handler: outgoing spoofed forwarding is overwritten, mapped
IPv6 is normalized, and the configured single retry is observed for each request. No external HTTP
request is made. Input data is synthetic. The same sample is restored, built and run
against packed candidates on Linux and Windows; candidate `--aot` validation also publishes and
runs its native executable. This validates these paths without claiming every API in the referenced
modules is NativeAOT-compatible.

Run locally with `dotnet run --project samples/HttpRequests/HttpRequests.Sample -c Release`.
