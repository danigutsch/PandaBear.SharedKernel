using System.Security.Claims;
using System.Text.Json;
using HttpRequests.Sample;
using PandaBear.AspNetCore;
using PandaBear.HttpClients;
using PandaBear.Idempotency;

var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.SubjectClaimType, "sample-subject")], "sample"));
var subject = principal.GetSubjectId() ?? throw new InvalidOperationException("Subject lookup failed.");
using var payload = JsonDocument.Parse("{\"amount\":100.00}");
var fingerprint = IdempotencyRequestFingerprint.CreateJson(payload.RootElement, RequestJsonContext.Default.JsonElement, subject);
if (fingerprint != IdempotencyRequestFingerprint.Create(subject, "{\"amount\":100}"))
{
    throw new InvalidOperationException("Typed request fingerprint changed its framing contract.");
}

var submission = new IdempotentSubmission<string>();
string? originalKey = null;
var first = true;
await submission.Submit("sample request", (key, _) =>
{
    originalKey ??= key;
    if (key != originalKey) { throw new InvalidOperationException("Retry changed its idempotency key."); }
    var kind = first ? HttpOutcomeKind.TransportFailure : HttpOutcomeKind.Succeeded;
    first = false;
    return Task.FromResult(new HttpOutcome<string> { Kind = kind, Value = fingerprint });
}, CancellationToken.None);
var retried = await submission.Retry(CancellationToken.None);
if (retried?.Kind != HttpOutcomeKind.Succeeded || submission.HasPending)
{
    throw new InvalidOperationException("Explicit retry failed.");
}
