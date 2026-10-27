# ASP.NET Core error and security telemetry

`PandaBear.SharedKernel.AspNetCore` offers two opt-in registrations that make unexpected errors and
security events observable without exposing exception text, tokens or claims. Packages remain preview
(prerelease). Nothing here is on by default.

## Register

```csharp
builder.Services.AddUnhandledExceptionHandler();   // IExceptionHandler + ProblemDetails + counter
builder.Services.AddSecurityEventTelemetry();      // JwtBearer events + rate-limit rejections

var app = builder.Build();
app.UseExceptionHandler();                         // required so the framework calls the handler
```

Export the meter with the host's OpenTelemetry (or other `System.Diagnostics.Metrics`) configuration by
adding the meter name `PandaBear.AspNetCore` (`AspNetCoreTelemetry.MeterName`).

`AddSecurityEventTelemetry` wraps, after all other configuration, the `JwtBearerEvents` of every
scheme and `RateLimiterOptions.OnRejected`. Existing handlers keep running after the report, and the
existing `AddApiSecurity`, `AddApiBearerAuthentication` and `AddRemoteIpFixedWindowPolicy` helpers are
unchanged.

Not instrumented, by design: a scheme that sets `EventsType`; a `JwtBearerEvents` subclass that overrides the
virtual methods (`AuthenticationFailed`, `Challenge`, `Forbidden`) without calling the base, because the wrapper
sits on the `OnAuthenticationFailed`, `OnChallenge` and `OnForbidden` delegates; and rate limiting configured only
through the `UseRateLimiter(RateLimiterOptions)` overload, because that options instance never passes through
dependency injection. Use `AddRateLimiter` for the limiter you want counted. A `JwtBearerEvents` instance shared by
several schemes is wrapped once and reports the scheme that raised each event.

### Telemetry never breaks the host

Every report call is contained. A failing logger, meter factory or metric listener cannot stop the request, the
host's own JwtBearer handler, its own `OnRejected` callback or the problem response. The failure is written to
standard error as one line holding only the exception type name (and breaks into an attached debugger), never its
message, a token or a claim. Reports run before the host's own handler.

## Unhandled exceptions

Register the handler in API hosts only. It is a separate opt-in registration and is not part of any
default or service-defaults entry point. Registered `IExceptionHandler` instances run before the
`UseExceptionHandler("/Error")` page, so a UI host that renders an error page must not register it; such a
host can still call `UnhandledExceptionReporter.Report` from its own error endpoint. The route template is
read from `IExceptionHandlerFeature.Endpoint`, because the exception middleware clears the request endpoint.

The handler answers `500` with an RFC 9457 `application/problem+json` body (`status`, `title`, `type`,
`traceId`) and never includes the exception message, type or stack. It logs one record per exception and
counts it. `traceId` is the current `Activity.Id` (a W3C `traceparent`-style value such as
`00-<trace-id>-<span-id>-01`), or the request's `TraceIdentifier` when there is no activity.

What it does not count:

- A cancellation caused by the client aborting the request: the handler returns `false`.
- A `BadHttpRequestException` (Kestrel 400, 413, 431 and similar; its `StatusCode` defaults to 400): this is a
  client error. The handler answers with that exception's own status and a generic problem body, and does not log
  or count it as an unhandled server exception.
- An exception thrown after the response started. The framework's exception middleware never calls handlers in that
  case (`ExceptionHandlerMiddlewareImpl.HandleException` in dotnet/aspnetcore, `src/Middleware/Diagnostics/src/ExceptionHandler`,
  checked at commit `e7c22bdd723c71fa0cf9269666541bc4406e2136`: when `Response.HasStarted` it logs, records its own
  metric and rethrows without calling any `IExceptionHandler`). To count a streaming or late failure, call
  `UnhandledExceptionReporter.Report(httpContext, exception, handled: false)` from your own `catch`.

`AddUnhandledExceptionHandler` also calls `AddProblemDetails()`, a global registration: it makes the status-code and
exception middleware write problem details for every endpoint of the host, not only this handler's responses. Review
that before enabling it in a host that already customizes its error bodies.

| Signal | Name | Values |
| --- | --- | --- |
| Log event | `EventId` 4001 `UnhandledException`, Error | `ErrorType`, `RouteTemplate`, `StatusCode`, `TraceId`; no exception object |
| Counter | `unhandled_exceptions_total` | `error_type` (exception type `Name`, with `AggregateException` and `TargetInvocationException` unwrapped to their first inner exception), `route_template` (matched pattern or `unmatched`), `handled` (bool) |

Another component can report with the same series. Resolve `UnhandledExceptionReporter` and call
`Report(exception, routeTemplate, statusCode, traceId, handled: true)`, for example from a Blazor circuit
error handler. `routeTemplate` must be a route template such as `/orders/{id}`, never a URL: a query string or
fragment is dropped, and a blank value, a value that looks like a URI (contains a scheme) or one longer than 256
characters is reported as `unmatched`, so a caller mistake cannot leak a path, token or unbounded label values.

## Authentication and rate limiting

| Signal | Name | Values |
| --- | --- | --- |
| Log event | `EventId` 4101 `AuthenticationEvent`, Warning (Information for a `challenge` with `missing_token`, which is routine anonymous traffic) | `SecurityEvent`, `Reason`, `Scheme`, `RouteTemplate`, `TraceId` |
| Counter | `authentication_events_total` | `event` = `authentication_failed`, `challenge`, `forbidden`; `reason`; `scheme` |
| Log event | `EventId` 4102 `RateLimitRejection`, Warning | `Policy`, `StatusCode`, `RouteTemplate`, `TraceId` |
| Counter | `rate_limit_rejections_total` | `policy` (the endpoint's `EnableRateLimiting` name, else `none`) |

The `policy` label comes from the matched endpoint's metadata. A rejection by a global limiter (`GlobalLimiter`) has no
policy of its own: it is labelled `none`, or with the endpoint's policy name when the endpoint also carries one, so the label
cannot distinguish the two. `scheme` is the authentication scheme that raised the event.

`reason` is one of `missing_token`, `token_expired`, `token_not_yet_valid`, `invalid_signature`,
`signing_key_not_found`, `invalid_audience`, `invalid_issuer`, `malformed_token`, `invalid_token`,
`authentication_error` or, for `forbidden`, `insufficient_permission`. Tokens, claims, client addresses
and exception messages are never logged or used as tags. A request with an invalid token raises both
`authentication_failed` and `challenge`; alert on one event value, not the sum.

## Alert rule templates

These are templates, not defaults. Copy them, then choose every value in angle brackets for your
service. The metric names assume an OpenTelemetry-to-Prometheus export that keeps the instrument name
and the tags. Exporters differ: the OpenTelemetry Prometheus conventions append `_total` to a monotonic counter
unless the name already ends in it (these instrument names already do, so they stay unchanged), and some pipelines
add unit suffixes or rewrite dots. Query your backend for the stored names first, then adjust every selector
below (for example `unhandled_exceptions` if your pipeline strips `_total`). The `job` label stands for whatever
label identifies the service in your setup, and `{{ ... }}` is the alert annotation templating.

A counter series does not exist until its first increment, so a brand-new series starts at 1 and `increase()` or
`rate()` over a window sees only one sample. The first-seen rule below therefore tests series presence with
`max_over_time` instead of using `increase()`; the spike rule pads missing series with zero.

```yaml
# TEMPLATE - not a recommendation. Choose each <parameter> before use.
groups:
  - name: <service>-errors
    rules:
      # 5xx ratio over a window. Needs an HTTP request counter that has a status code label;
      # <http_requests_metric> and <status_label> depend on your instrumentation (for the ASP.NET Core
      # server metrics exported to Prometheus, typically http_server_request_duration_seconds_count and
      # http_response_status_code). Both sides are sums of rates over the same window.
      - alert: <Service>High5xxRatio
        expr: |
          sum by (job) (rate(<http_requests_metric>{job="<job>",<status_label>=~"5.."}[<window>]))
            /
          sum by (job) (rate(<http_requests_metric>{job="<job>"}[<window>]))
            > <ratio_threshold>
        for: <for_duration>
        labels: { severity: <severity> }
        annotations:
          summary: "5xx ratio above <ratio_threshold> for {{ $labels.job }}"

      # First-seen error_type: a series present in the last <recent> that did not exist in the <baseline> before it.
      # Presence, not increase(): a new counter series starts at 1 and increase() would miss it.
      - alert: <Service>NewErrorType
        expr: |
          sum by (error_type) (max_over_time(unhandled_exceptions_total{job="<job>"}[<recent>]))
            unless
          sum by (error_type) (max_over_time(unhandled_exceptions_total{job="<job>"}[<baseline>] offset <recent>))
        labels: { severity: <severity> }
        annotations:
          summary: "New unhandled error type {{ $labels.error_type }}"

      # Spike in authentication failures against the same window one week earlier. A job with no series a week ago
      # is padded with zero so that the floor, not a missing series, decides.
      - alert: <Service>AuthFailureSpike
        expr: |
          sum by (job) (rate(authentication_events_total{job="<job>",event="authentication_failed"}[<window>]))
            > <multiplier> *
          (
            (
              sum by (job) (rate(authentication_events_total{job="<job>",event="authentication_failed"}[<window>] offset 7d))
              or
              sum by (job) (rate(authentication_events_total{job="<job>",event="authentication_failed"}[<window>])) * 0
            )
            + <floor>
          )
        for: <for_duration>
        labels: { severity: <severity> }
        annotations:
          summary: "Authentication failures for {{ $labels.job }} are {{ $value }}/s, above <multiplier> times last week"

      # Optional: sustained rate-limit rejections for one policy.
      - alert: <Service>RateLimitRejections
        expr: sum by (job, policy) (rate(rate_limit_rejections_total{job="<job>"}[<window>])) > <rate_threshold>
        for: <for_duration>
        labels: { severity: <severity> }
        annotations:
          summary: "Policy {{ $labels.policy }} is rejecting {{ $value }} requests/s for {{ $labels.job }}"
```

For the first-seen rule, `<baseline>` is typically `7d` and `<recent>` a few minutes to an hour; the rule fires once
per new `error_type` while it is inside the `<recent>` window. In a VictoriaMetrics-compatible `vmalert` the same
expressions run as MetricsQL; `max_over_time`, `offset`, `unless` and `or` are valid in both. `<floor>` stops a zero
baseline from alerting on a single failure; choose it from your normal traffic. A `rate()` needs two samples, so the
very first failures of a new series are not seen by the spike rule; the first-seen rule is the one for brand-new series.
