# SharedKernel.Testing.Http

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable HTTP response and message-handler helpers for SharedKernel test suites.

## Usage

Construct `HttpClient` with `TestHttpMessageHandler`, enqueue HTTP or JSON responses, and inspect its captured requests. `HttpResponseFactory.Json` creates explicit test responses. This test handler avoids network access; consumers retain response-disposal and contract assertions.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Http`. C# namespace: `PandaBear.Testing.Http`. Assembly/project identity: `SharedKernel.Testing.Http`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
