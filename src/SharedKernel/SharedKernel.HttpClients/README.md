# PandaBear.SharedKernel.HttpClients

Reusable HTTP client helpers, validation responses, defaults, and telemetry.

## Usage

Call `AddHttpClientDefaults()` when composing reusable HTTP defaults. Use `HttpOutcomeMapper.Send` to map bounded HTTP responses into `HttpOutcome` or `HttpOutcome<T>`; `ContractHttpValidation` supports validation-error exceptions when that calling convention is required. `ProgressStreamContent` reports transferred bytes, while `UploadOperationState` tracks operation identity and terminal state. Keep credentials, endpoint configuration, and application retry policy in the consumer; propagate cancellation and retain response-size limits.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.HttpClients --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using System.Net;
using System.Text.Json.Serialization;
using PandaBear.HttpClients;

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

Package ID: `PandaBear.SharedKernel.HttpClients`. C# namespace: `PandaBear.HttpClients`. Assembly/project identity: `SharedKernel.HttpClients`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
