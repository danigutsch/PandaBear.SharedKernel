using System.Security.Claims;
using PandaBear.AspNetCore;
using PandaBear.HttpClients;
using PandaBear.Idempotency;

var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.SubjectClaimType, "sample-subject")], "sample"));
var subject = principal.GetSubjectId() ?? throw new InvalidOperationException("Subject lookup failed.");
_ = IdempotencyKey.From(Guid.CreateVersion7().ToString("N"));

var submission = new IdempotentSubmission<string>();
string? originalKey = null;
var first = true;
await submission.Submit("sample request", (key, _) =>
{
    originalKey ??= key;
    if (key != originalKey)
    {
        throw new InvalidOperationException("Retry changed its idempotency key.");
    }
    var kind = first ? HttpOutcomeKind.TransportFailure : HttpOutcomeKind.Succeeded;
    first = false;
    return Task.FromResult(new HttpOutcome<string> { Kind = kind, Value = subject });
}, CancellationToken.None);
var retried = await submission.Retry(CancellationToken.None);
if (retried?.Kind != HttpOutcomeKind.Succeeded || submission.HasPending)
{
    throw new InvalidOperationException("Explicit retry failed.");
}
