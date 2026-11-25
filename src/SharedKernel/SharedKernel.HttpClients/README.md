# PandaBear.SharedKernel.HttpClients

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable HTTP client helpers, validation responses, defaults, and telemetry.

## Usage

HttpClient defaults (standard resilience without retries of unsafe methods, then service discovery, and telemetry) come from
`AddApplicationDefaults` in `PandaBear.SharedKernel.ServiceDefaults`. Use `HttpOutcomeMapper.Send` to map bounded HTTP responses into `HttpOutcome` or `HttpOutcome<T>`; `ContractHttpValidation` supports validation-error exceptions when that calling convention is required. `ProgressStreamContent` reports transferred bytes, while `UploadOperationState` tracks operation identity and terminal state. Keep credentials, endpoint configuration, and application retry policy in the consumer; propagate cancellation and retain response-size limits.

## Standard resilience customization

Set `ServiceDefaultsOptions.ConfigureResilience` to customize the shared standard resilience
options without adding another handler. The callback configures defaults across clients; it does
not receive a per-client builder. Keep request-specific classification inside resilience predicates,
using the current request from `Polly.HttpResilienceContextExtensions.GetRequestMessage` rather
than capturing an ambient HTTP request during registration.

For example, `options.DisableRetryOnTooManyRequests()` makes a 429 answer final: it is neither
retried, which would spend more of a quota the server just reported exhausted, nor counted by the
circuit breaker, which would otherwise cut off every request to that server after a burst of quota
answers. Every other outcome goes to the predicates already configured. Pass a request predicate,
such as `request => request.RequestUri?.AbsolutePath.StartsWith("/api/public/") == true`, to limit
the rule to the routes of one quota-limited API; that classification stays application-owned. Automatic retries for unsafe HTTP methods
remain disabled after customization, including when a callback supplies a broader retry predicate.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.HttpClients --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using System.Net;
using System.Text.Json.Serialization;
using SharedKernel.HttpClients;

public static class Example
{
    public static Task<HttpOutcome> Delete(HttpClient client, Uri item, CancellationToken ct)
        => HttpOutcomeMapper.Send(client,
            new HttpRequestMessage(HttpMethod.Delete, item),
            HttpStatusCode.NoContent,
            ClientJson.Default.ContractValidationProblemDto, ct);
}

[JsonSerializable(typeof(ContractValidationProblemDto))]
public partial class ClientJson : JsonSerializerContext;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.HttpClients`. C# namespace: `SharedKernel.HttpClients`. Assembly/project identity: `SharedKernel.HttpClients`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Explicit idempotent submissions

`IdempotentSubmission<T>` retains one callback and a generated, validated `IdempotencyKey` value when an HTTP outcome cannot
establish whether the server committed the command. `Retry` invokes that same callback with the
same key. `Submit` and `Retry` return null while a request is busy; a pending command rejects
replacement submissions. Terminal success, validation, missing-resource and authorization/conflict
outcomes release the pending command. Transport failures, timeouts and unusable response bodies
retain it. Exceptions and cancellation also preserve the pending command while releasing the busy flag.

Capture immutable input snapshots in the callback, use server-enforced idempotency, and keep operation
labels free of sensitive data. Retries are explicit; the helper never schedules background requests
and does not persist pending state across restart. UI presentation and business approval remain
consumer responsibilities.

## Streamed response ownership

Send the request with `HttpCompletionOption.ResponseHeadersRead` and pass the
response to `HttpResponseBody.ReadOrNotFound`. A successful response supplies its
stream and media type to the caller's owner factory. The caller disposes the returned
owner when streaming ends. HTTP 404 returns null. Exceptions and missing responses
release the upstream response, including failures when opening or accepting the body.
