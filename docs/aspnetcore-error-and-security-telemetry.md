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
unchanged. A scheme that sets `EventsType` is not instrumented.

## Unhandled exceptions

Register the handler in API hosts only. It is a separate opt-in registration and is not part of any
default or service-defaults entry point. Registered `IExceptionHandler` instances run before the
`UseExceptionHandler("/Error")` page, so a UI host that renders an error page must not register it; such a
host can still call `UnhandledExceptionReporter.Report` from its own error endpoint. The route template is
read from `IExceptionHandlerFeature.Endpoint`, because the exception middleware clears the request endpoint.

The handler answers `500` with an RFC 9457 `application/problem+json` body (`status`, `title`, `type`,
`traceId`) and never includes the exception message, type or stack. It logs one record per exception and
counts it. It returns `false` for a cancellation caused by the client aborting the request.

| Signal | Name | Values |
| --- | --- | --- |
| Log event | `EventId` 4001 `UnhandledException`, Error | `ErrorType`, `RouteTemplate`, `StatusCode`, `TraceId`; no exception object |
| Counter | `unhandled_exceptions_total` | `error_type` (exception type full name), `route_template` (matched pattern or `unmatched`), `handled` (bool) |

Another component can report with the same series. Resolve `UnhandledExceptionReporter` and call
`Report(exception, routeTemplate, statusCode, traceId, handled: true)`, for example from a Blazor circuit
error handler.

## Authentication and rate limiting

| Signal | Name | Values |
| --- | --- | --- |
| Log event | `EventId` 4101 `AuthenticationEvent`, Warning | `SecurityEvent`, `Reason`, `Scheme`, `RouteTemplate`, `TraceId` |
| Counter | `authentication_events_total` | `event` = `authentication_failed`, `challenge`, `forbidden`; `reason`; `scheme` |
| Log event | `EventId` 4102 `RateLimitRejection`, Warning | `Policy`, `StatusCode`, `RouteTemplate`, `TraceId` |
| Counter | `rate_limit_rejections_total` | `policy` (the endpoint's `EnableRateLimiting` name, else `none`) |

`reason` is one of `missing_token`, `token_expired`, `token_not_yet_valid`, `invalid_signature`,
`signing_key_not_found`, `invalid_audience`, `invalid_issuer`, `malformed_token`, `invalid_token`,
`authentication_error` or, for `forbidden`, `insufficient_permission`. Tokens, claims, client addresses
and exception messages are never logged or used as tags. A request with an invalid token raises both
`authentication_failed` and `challenge`; alert on one event value, not the sum.

## Alert rule templates

These are templates, not defaults. Copy them, then choose every value in angle brackets for your
service. The metric names below assume an OpenTelemetry-to-Prometheus export that keeps the instrument
name and tags; check the names your backend actually stores before use. The `{{ ... }}` markers
belong to the alert annotation templating; the `job` label and the service selector are placeholders.

```yaml
# TEMPLATE - not a recommendation. Choose each <parameter> before use.
groups:
  - name: <service>-errors
    rules:
      # 5xx ratio over a window. Needs an HTTP request counter that has a status code label;
      # <http_requests_metric> and <status_label> depend on your instrumentation (for the ASP.NET Core
      # server metrics exported to Prometheus, typically http_server_request_duration_seconds_count and
      # http_response_status_code).
      - alert: <Service>High5xxRatio
        expr: |
          sum(rate(<http_requests_metric>{job="<job>",<status_label>=~"5.."}[<window>]))
            /
          sum(rate(<http_requests_metric>{job="<job>"}[<window>]))
            > <ratio_threshold>
        for: <for_duration>
        labels: { severity: <severity> }
        annotations:
          summary: "5xx ratio above <ratio_threshold> for {{ $labels.job }}"

      # A first-seen error_type: present in the last <recent> and absent from the <baseline> before it.
      - alert: <Service>NewErrorType
        expr: |
          sum by (error_type) (increase(unhandled_exceptions_total{job="<job>"}[<recent>]))
            > 0
          unless
          sum by (error_type) (
            increase(unhandled_exceptions_total{job="<job>"}[<baseline>] offset <recent>)
          ) > 0
        labels: { severity: <severity> }
        annotations:
          summary: "New unhandled error type {{ $labels.error_type }}"

      # Spike in authentication failures against a baseline of the same weekday window.
      - alert: <Service>AuthFailureSpike
        expr: |
          sum(rate(authentication_events_total{job="<job>",event="authentication_failed"}[<window>]))
            > <multiplier> *
          (sum(rate(authentication_events_total{job="<job>",event="authentication_failed"}[<window>] offset 7d)) + <floor>)
        for: <for_duration>
        labels: { severity: <severity> }
        annotations:
          summary: "Authentication failures are {{ $value }}/s, above <multiplier> times last week"

      # Optional: sustained rate-limit rejections for one policy.
      - alert: <Service>RateLimitRejections
        expr: sum by (policy) (rate(rate_limit_rejections_total{job="<job>"}[<window>])) > <rate_threshold>
        for: <for_duration>
        labels: { severity: <severity> }
```

For the first-seen rule, `<baseline>` is typically `7d` and `<recent>` a few minutes to an hour. In a
VictoriaMetrics-compatible `vmalert` the same expressions run as MetricsQL; the `offset` and `unless`
forms above are valid in both. `<floor>` stops a zero baseline from alerting on a single failure;
choose it from your normal traffic. Metric names in Prometheus form may carry a unit or `_total` suffix
that your exporter adds; adjust the selectors to match.
