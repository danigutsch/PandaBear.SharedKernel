# HTTP request primitives sample

Exercises `GetSubjectId`, validated idempotency keys, and explicit retry with an
unchanged idempotency key. Input data is synthetic. The same sample is restored, built and run
against packed candidates on Linux and Windows; candidate `--aot` validation also publishes and
runs its native executable. This validates these paths without claiming every API in the referenced
modules is NativeAOT-compatible.

Run locally with `dotnet run --project samples/HttpRequests/HttpRequests.Sample -c Release`.
