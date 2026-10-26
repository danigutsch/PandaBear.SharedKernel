# SharedKernel.Aspire.Hosting.K6

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable .NET Aspire hosting extensions that run one k6 script as an explicit-start container, with OAuth 2.0
client-credentials authentication and a results directory.

The package builds on `CommunityToolkit.Aspire.Hosting.k6` and pins the `grafana/k6` image by tag and digest.

```csharp
var k6 = builder.AddK6Scenario("admin-smoke", scriptDirectory, "scenarios/admin-smoke.js", resultsDirectory)
    .WithReference(api)
    .WaitFor(api)
    .WithK6ClientCredentials(identityProvider, "/realms/app/protocol/openid-connect/token", "k6", clientSecret);
```

## Behavior

- The container starts only when requested from the Aspire dashboard or CLI.
- `scriptDirectory` mounts read-only at `/scripts`, the helper library at `/k6lib`, and `resultsDirectory` writable at
  `/results`. The helper library is written to the per-user data folder. On Linux and macOS a results directory that
  the call creates is made writable for the container user with the sticky bit set; an existing results directory keeps
  its mode and must already allow the container user to write.
- k6 writes its own log to `k6.log` and the script's `console` output to `k6-console.log` in `resultsDirectory`,
  so a failed run can be read on the host without a container log driver. Both files are emptied when the AppHost
  builds the resource and appended to by each run. The library scripts never print the token or
  the client secret; treat both log files like the summary file when a scenario script logs more.
- The script must stay under `scriptDirectory`. No `--vus` or `--duration` flag is passed, so the script's own `options`
  decide the load.
- `WithK6ClientCredentials` waits for the identity provider and passes `SK_K6_TOKEN_URL`, `SK_K6_CLIENT_ID`,
  `SK_K6_CLIENT_SECRET`, and `SK_K6_TOKEN_HOST`. The token request reaches the identity provider through the container
  network and carries the host-side `localhost` address as its `Host` header, so the token's issuer matches the issuer
  that services validate. The secret is an Aspire secret parameter. The container receives the values as environment
  variables; they never appear in a file or in k6 output.

## k6 helper library

| Module | Export | Use |
|---|---|---|
| `/k6lib/auth.js` | `getAccessToken()` | Call from `setup()`. Requests a client-credentials token and throws on a non-200 response. It never logs the token or the secret. |
| `/k6lib/auth.js` | `bearerHeaders(token, extraHeaders)` | Builds the `Authorization` header. |
| `/k6lib/summary.js` | `createSummaryHandler(name)` | Export it as `handleSummary` to write `/results/<name>-summary.json`. It drops `setup_data`, which holds the access token. |

```javascript
import http from 'k6/http';
import { getAccessToken, bearerHeaders } from '/k6lib/auth.js';
import { createSummaryHandler } from '/k6lib/summary.js';

export function setup() {
  return getAccessToken();
}

export default function (token) {
  http.get(`${__ENV.API_BASE_URL}/items`, { headers: bearerHeaders(token) });
}

export const handleSummary = createSummaryHandler('items-smoke');
```

A failed token request stops the run before any load starts, and a breached threshold makes k6 exit with a non-zero
code.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.K6`. C# namespace: `PandaBear.Aspire.Hosting.K6`. Assembly/project identity: `SharedKernel.Aspire.Hosting.K6`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
