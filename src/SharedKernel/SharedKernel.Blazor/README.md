# SharedKernel.Blazor

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Accessible Blazor helpers for HTTP outcome messages, server validation, error summaries, handled-failure reporting, and upload status. The components are plain C# classes with no CSS or JavaScript requirement; style them with the `class` attribute they pass through.

## HTTP failures and server validation

Show server failures in two tiers and never join server text into one message.

1. Field-keyed validation errors go to the form through `ServerValidationBinder`, which uses a `ValidationMessageStore`. Only fields named in `ServerValidationOptions.KnownFields` are shown (matched without regard to case). Names and messages are bounded; blank, multi-line, or stack-trace-like messages become the fallback field message. A field's messages are cleared when that field changes, and all server messages are cleared when a new validation request starts (an `EditForm` submit), so unchanged data can be submitted again. Duplicate messages are shown once, and the per-field cap holds across case-variant keys. Dispose the binder with the form.
2. Everything else shows fixed copy that you choose. `HttpOutcomeMessages` maps an `HttpOutcomeKind` to your copy and never reads `HttpOutcome.Message` or `ValidationErrors`. `ServerValidationResult.HasUnplacedErrors` tells you when anything could not be shown on a field: unknown or model-level keys, keys beyond `MaxFields`, or messages beyond `MaxMessagesPerField`. All `Max*` options must be positive. Display text drops Unicode format characters (bidirectional overrides, zero-width characters), and screening rejects text longer than four times `MaxMessageLength` before any pattern runs.

```csharp
private static readonly HttpOutcomeMessages Messages = new HttpOutcomeMessages("We could not save the customer. Try again later.")
    .With(HttpOutcomeKind.NotFound, "That customer no longer exists.")
    .With(HttpOutcomeKind.Conflict, "Someone else changed this customer. Reload and try again.");

_binder = new ServerValidationBinder(_editContext, new ServerValidationOptions(["Name", "Email"], "This value is not valid."));

var outcome = await Api.Save(_model, token);
if (outcome.Kind == HttpOutcomeKind.ValidationProblem)
{
    var result = _binder.Apply(outcome);
    _summary = result.ToSummaryItems(field => "customer-" + field);
    _error = result.HasUnplacedErrors ? Messages.For(outcome) : null;
}
else if (outcome.Kind != HttpOutcomeKind.Succeeded)
{
    _error = Messages.For(outcome);
}
```

`ErrorSummary` renders a labelled `section` (`tabindex="-1"`) with a heading and fragment links to the fields, and takes focus whenever a new `Items` list instance arrives, so pass a fresh list for every submission, even with identical content, and keep the same list across unrelated re-renders. Each link is the current page address without its fragment plus `#id`, so a document `<base href>` cannot redirect it. The links carry `target="_top"` so Blazor's link interception, which scrolls to a fragment without moving focus, leaves them to the browser's native fragment navigation, which focuses the target. The target element must therefore be an input or otherwise focusable (for example `tabindex="-1"`). Give each field a stable `id`, and show a message beside each field with `ValidationMessage`.

```razor
<ErrorSummary Title="There is a problem" Items="_summary" class="error-summary" />
```

## Handled UI failures

Blazor Server component exceptions do not reach the ASP.NET Core exception handler, and code that catches them often shows a message and nothing else. `IUiFailureReporter.Report(exception, operation)` logs `Handled UI failure in {Operation}: {ExceptionType} (trace {TraceId})` under `EventId` 7001 (`HandledUiFailure`). The exception object, message, stack, and data are never logged. Operation names must be code-chosen constants; unsafe or overlong names are logged as `unknown`.

Each report also increments the counter `handled_ui_failures_total` on the meter `PandaBear.Blazor` (`UiFailureReporter.MeterName`), tagged `error_type` (exception type name, with `AggregateException` and `TargetInvocationException` unwrapped to their first inner exception) and `operation` (the sanitized operation name). Tag cardinality is bounded by your call sites; messages, trace ids, and user data are never tags. The counter is separate from `unhandled_exceptions_total` in `SharedKernel.AspNetCore`, because a page failure has no route template. Metrics are fail-safe: a throwing meter factory or listener writes the exception type name to standard error and never breaks reporting or the log. The reporter needs an `IMeterFactory`, which the ASP.NET Core host and `AddMetrics()` register. Register the meter for export, for example `metrics.AddMeter(UiFailureReporter.MeterName)` with OpenTelemetry.

```csharp
builder.Services.AddUiFailureReporting();
```

```razor
<RecoveryErrorBoundary RecoveryMessage="Something went wrong. Reload the page to continue."
                       RecoverLabel="Try again" OperationName="app.root">
    @Body
</RecoveryErrorBoundary>
```

`RecoveryErrorBoundary` reports the fault, renders only your fixed copy in a focusable `role="alert"` element and moves focus to it, and offers a button to render the child again when you supply `RecoverLabel`. After that button is used, focus moves to an empty `tabindex="-1"` anchor placed before the child's content.

## Upload status

`UploadStatus` shows an `UploadOperationState`. It always renders a `section` named by `Label` and an initially empty `role="status"` element (an implicit polite live region), so the first announcement is read. The status holds the state's announcement (the state's ten-percent steps throttle what assistive technology reads) and, after a failure, the failure reason. It also renders a determinate `progress` only while uploading and an indeterminate one while queued, retrying, or processing, and a cancel button named after the file. When the cancel button had focus and disappears, focus moves to the section; focus changes on the button cause no render. The component renders when its parent re-renders after mutating the state, and it does not cancel transport work.

```razor
<UploadStatus State="_upload" Label="Customer import upload status" OnCancel="CancelUpload" />
```

File names and failure messages are encoded and truncated to 200 characters. The default English strings, `CancelText` and `ProgressLabel`, are parameters for localization; the announcement text comes from `UploadOperationState`.

## Notes

- Link targets in `ErrorSummary` are fragment links on the current page; browsers move focus to a focusable target of a fragment navigation.
- Depends on the `Microsoft.AspNetCore.Components.Web` package and `PandaBear.SharedKernel.HttpClients`, not on the ASP.NET Core shared framework; see the [package decision](../../../docs/adr/20261027-blazor-helpers-package.md).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor`. C# namespace: `PandaBear.Blazor`. Assembly/project identity: `SharedKernel.Blazor`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
