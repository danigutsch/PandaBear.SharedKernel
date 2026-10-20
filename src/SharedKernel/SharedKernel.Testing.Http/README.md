# SharedKernel.Testing.Http

Reusable HTTP response and message-handler helpers for SharedKernel test suites.

## Usage

Construct `HttpClient` with `TestHttpMessageHandler`, enqueue HTTP or JSON responses, and inspect its captured requests. `HttpResponseFactory.Json` creates explicit test responses. This test handler avoids network access; consumers retain response-disposal and contract assertions.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Http`. C# namespace: `PandaBear.Testing.Http`. Assembly/project identity: `SharedKernel.Testing.Http`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
