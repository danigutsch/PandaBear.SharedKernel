# SharedKernel.AspNetCore.Authentication.Bff

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Backend-for-frontend (BFF) token handling for ASP.NET Core and Blazor Server apps that sign users in with OpenID Connect
and call APIs on their behalf. The browser holds only a cookie; tickets and tokens stay on the server.

It depends only on the ASP.NET Core shared framework and the first-party
`Microsoft.AspNetCore.Authentication.OpenIdConnect` package. It does not use Duende.AccessTokenManagement or
IdentityModel. It works with any OpenID Connect provider whose discovery document names a token endpoint; Keycloak is the
tested target for token exchange.

## What is inside

- `ServerSideTicketStore` and `AddServerSideTicketStore`: an `ITicketStore` over `IDistributedCache`. The cookie carries
  only an opaque random key under a configurable prefix (`ServerSideTicketStoreOptions.KeyPrefix`). Each ticket is
  protected with Data Protection under a purpose bound to its key, so an entry copied to another key cannot be read. The
  cache entry expires with the ticket and never later than `AbsoluteLifetime` after sign-in, even when the cookie slides.
  An optional `SlidingExpiration` evicts idle entries. An unreadable or expired ticket is removed and signs the user out.
- `UserTokenManager` and `AddUserTokenManagement`: per-session access, refresh and ID tokens, kept in `IDistributedCache`
  and protected with Data Protection, keyed by an opaque session id claim (`BffClaimTypes.SessionId`). The registration
  hooks the OpenID Connect and cookie schemes:
  - a validated login starts the session from the token endpoint response (`SaveTokens` is not needed);
  - `GetAccessToken` refreshes the access token with the refresh_token grant when it is within `RefreshBeforeExpiry`
    (default one minute, which also absorbs clock skew) of expiring. Concurrent callers of one session share one refresh.
    A rotated refresh token replaces the old one; an answer without one keeps it;
  - a refresh rejected with `invalid_grant`, or an expired token without a refresh token, ends the token session and
    returns the typed failure `UserTokenFailure.RefreshFailed`. The cookie is then rejected and signed out on the next
    request (`RejectCookieWithoutTokenSession`). An identity provider that fails, times out or answers with another OAuth
    error keeps the tokens for a retry and returns the stored access token while it has not expired, otherwise
    `IdentityProviderUnavailable`;
  - cookie sign-out removes the tokens and revokes the refresh token (RFC 7009) when discovery names a
    `revocation_endpoint` (`RevokeRefreshTokenOnSignOut`).
- `AddUserAccessTokenHandler()`: an `HttpClient` handler that attaches the current user's access token.
- `AddTokenExchangeHandler(audience, scope)`: an `HttpClient` handler that exchanges the user's access token for one
  audience (RFC 8693) and attaches the result. Exchanged tokens are cached per session and audience until they near
  expiry, and only while the user's access token is the one they were exchanged from. `UserTokenManager.GetAudienceAccessToken`
  does the same without a handler.
- `AddBlazorServerCurrentUser()`: lets the handlers find the user of a Blazor Server circuit, where no HTTP request is
  current, by publishing the circuit's services to handlers created by `IHttpClientFactory`.
- `IUserSessionLock`: serializes refresh, exchange and sign-out per session. The default locks in-process. Register a
  distributed implementation (for example a PostgreSQL advisory lock) when one session's requests can reach several
  instances at once.
- `IUserTokenCacheConcurrency`: register a singleton adapter for the same cache backing store to make refresh writes
  and cleanup atomic across processes. The protected value read under the session lock serves as the opaque version.
  Atomically update a matching, active entry with its original session expiration, and delete a matching entry only.
  A lost comparison makes the refresh use the currently persisted access token if it is still valid. Otherwise the
  refresh returns the current session or token failure. Without this adapter, refresh uses a value recheck under the
  session lock.

When no token can be attached, the handlers throw `UserTokenUnavailableException` with the `UserTokenFailure` and send
nothing.

### Security properties

- Tokens never reach logs or exception messages. Log events carry the operation, the HTTP status, and a well-formed OAuth
  `error` code; `error_description` and response bodies are not logged. `UserTokenSet` and `UserTokenResult` redact
  tokens in `ToString`.
- Tokens, tickets and exchanged tokens are protected at rest with Data Protection, each under a key-bound purpose, so
  sessions cannot read each other's entries. Exchanged entries keep only a SHA-256 fingerprint of the source token.
- Calls to the identity provider use the named client `UserTokenManagementDefaults.IdentityProviderHttpClientName` from
  `IHttpClientFactory`, with `IdentityProviderTimeout` (default ten seconds), no automatic redirects, a 256 KiB answer
  limit, and no additional handlers. Retry handlers added by `ConfigureHttpClientDefaults` are removed from it, because
  a replayed refresh_token grant can make the identity provider revoke a rotated refresh token.
- Token and revocation endpoints must be HTTPS unless the OpenID Connect scheme sets `RequireHttpsMetadata = false`.
- A refresh that has started completes even when the caller's request is aborted, so a rotated refresh token is not lost.
- `TimeProvider` drives every expiry decision.

## Install and wire

```sh
dotnet add package PandaBear.SharedKernel.AspNetCore.Authentication.Bff
```

```csharp
using PandaBear.AspNetCore.Authentication.Bff;

builder.Services.AddDataProtection().PersistKeysTo...(); // shared by every instance
builder.Services.AddStackExchangeRedisCache(...);        // or any IDistributedCache

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options => options.ExpireTimeSpan = TimeSpan.FromHours(8))
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://idp.example/realms/app";
        options.ClientId = "app-bff";
        options.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
        options.ResponseType = "code";
        options.Scope.Add("offline_access");
    });

builder.Services.AddServerSideTicketStore(configure: options => options.KeyPrefix = "app-ticket:");
builder.Services.AddUserTokenManagement(options => options.CacheKeyPrefix = "app-token:");
builder.Services.AddBlazorServerCurrentUser(); // Blazor Server apps only

builder.Services.AddHttpClient<IOrdersClient, OrdersClient>(client => client.BaseAddress = new("https://orders"))
    .AddTokenExchangeHandler("orders-api", scope: "orders-api");
builder.Services.AddHttpClient<IProfileClient, ProfileClient>(client => client.BaseAddress = new("https://profile"))
    .AddUserAccessTokenHandler();
```

Neither scheme may set `EventsType`: the registration wraps the `Events` instance, and an `EventsType` would bypass it, so
startup fails with an `InvalidOperationException`. Assign an events instance to `Events` instead; its handlers still run.

## Ticket store failure metrics

SharedKernel service defaults already collect the `SharedKernel.AspNetCore.Authentication.Bff` meter. A host with
custom OpenTelemetry configuration can subscribe with `AddMeter("SharedKernel.AspNetCore.Authentication.Bff")`.
The `sharedkernel.bff.session_ticket.failures` counter has one dimension named
`sharedkernel.bff.session_ticket.operation` with the values `protect`, `unprotect`, `cache_get`, `cache_set`, and `cache_remove`.

An unreadable cached ticket counts as an `unprotect` failure. A failed cleanup counts separately as
`cache_remove`. Successful operations, normal misses, expired tickets, and cancellation do not increment
the counter.

## Migrating the ViajantesTurismo Management BFF off Duende

The old Management web project (`src/ViajantesTurismo.Management.Web`) is the behaviour reference. Each class maps to this
package as follows.

| Old repository | This package |
| --- | --- |
| `ProtectedDistributedTicketStore` | `ServerSideTicketStore` via `AddServerSideTicketStore(configure: o => { o.KeyPrefix = "management-ticket:"; o.AbsoluteLifetime = TimeSpan.FromHours(8); })`. Keys are still 32 random bytes, Base64Url, behind a prefix; protection is still key-bound; unreadable or expired tickets are still removed. The Data Protection purpose changed, so existing sessions sign in once more after the switch. |
| `ManagementCookieTicketCacheEntry` (EF entity of the PostgreSQL-backed cache) | Unchanged: it is the storage of the application's `IDistributedCache`, which both stores keep using. |
| `ProtectedDistributedUserTokenStore` (Duende `IUserTokenStore`, ~530 lines) | `UserTokenManager`. One protected token set per session replaces Duende's per-scope/resource entries: per-resource tokens come from token exchange. The `:revoked` marker and `ClearAll` become `SignOut`, which removes the tokens under the session lock. `ExecuteForActiveSession` is internal locking. DPoP stays unsupported. |
| PostgreSQL advisory lock inside `ProtectedDistributedUserTokenStore` | An application `IUserSessionLock` built on `PandaBear.Npgsql`'s transaction advisory lock, registered before `AddUserTokenManagement`; or the in-process default when sessions stay on one instance. |
| `ProtectedDistributedAudienceTokenStore` | Internal exchanged-token cache of `UserTokenManager`: same source-token fingerprint binding, refresh one minute before expiry (`RefreshBeforeExpiry`), expiry capped at the session's end, removal at sign-out. The fixed audience list becomes the audiences registered with `AddTokenExchangeHandler`. |
| `KeycloakAudienceTokenExchangeHandler` and `AddKeycloakAudienceTokenExchangeHandler(audience)` | `AddTokenExchangeHandler(audience, scope: audience)`. The request, the answer checks (Bearer, `issued_token_type` access token, a different token, a positive lifetime) and the HTTPS token-endpoint rule are kept. Drop the preceding `AddUserAccessTokenHandler()`: the handler gets the user token itself. |
| `ManagementAuthenticationDefaults.KeycloakTokenExchangeHttpClientName` (no redirects) | `UserTokenManagementDefaults.IdentityProviderHttpClientName`: no redirects, plus a timeout and no retry handlers. |
| `ManagementTokenSession` and the `urn:viajantes:management:token-session-*` claims | Internal session reading of `BffClaimTypes.SessionId` and `BffClaimTypes.SessionExpiresAt`; lifetime from `UserTokenManagementOptions.SessionLifetime`. |
| `ManagementOpenIdConnectEvents` (stores tokens on `TokenValidated`) | Built in: `AddUserTokenManagement` starts the session after a validated login. Remove `EventsType = typeof(ManagementOpenIdConnectEvents)`. A missing refresh token is now allowed; the session then ends when the access token expires. |
| `ManagementCookieAuthenticationEvents` (cleanup on `SigningOut`) | Built in: cookie sign-out calls `UserTokenManager.SignOut`, which also revokes the refresh token. Remove `EventsType = typeof(ManagementCookieAuthenticationEvents)`. |
| `AddOpenIdConnectAccessTokenManagement().AddBlazorServerAccessTokenManagement<ProtectedDistributedUserTokenStore>()` | `AddUserTokenManagement()` and `AddBlazorServerCurrentUser()`. |
| `.AddUserAccessTokenHandler().AddKeycloakAudienceTokenExchangeHandler(ApiAudienceNames.Admin)` | `.AddTokenExchangeHandler(ApiAudienceNames.Admin, scope: ApiAudienceNames.Admin)` |

The old tests map to `SharedKernel.AspNetCore.Authentication.Bff.Tests`: ticket store round trips, tampering and expiry
(`ServerSideTicketStoreTests`); token storage, refresh, coalescing, rotation, failure and isolation (`UserTokenManagerTests`);
exchange caching and answer validation (`TokenExchangeTests`); sign-out (`SignOutTests`); event wiring
(`AuthenticationEventTests`); and the handlers (`TokenHandlerTests`, `CurrentUserAccessorTests`). Keep the old repository's
PostgreSQL-hosted tests for the application's own lock and cache.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore.Authentication.Bff`. C# namespace: `PandaBear.AspNetCore.Authentication.Bff`.
Assembly/project identity: `SharedKernel.AspNetCore.Authentication.Bff`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
