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

Each report also increments the counter `handled_ui_failures_total` on the meter `PandaBear.Blazor` (`UiFailureReporter.MeterName`), tagged `error_type` (exception type name, with `AggregateException` and `TargetInvocationException` unwrapped to their first inner exception) and `operation` (the sanitized operation name). Tag cardinality is bounded by your call sites; messages, trace ids, and user data are never tags. The counter is separate from `unhandled_exceptions_total` in `SharedKernel.AspNetCore`, because a page failure has no route template. Metrics are fail-safe: a throwing meter factory or listener writes the exception type name to standard error and never breaks reporting or the log. `AddUiFailureReporting()` calls `AddMetrics()` itself for the `IMeterFactory` the reporter needs; the call is idempotent, so callers that already register metrics are unaffected. Register the meter for export, for example `metrics.AddMeter(UiFailureReporter.MeterName)` with OpenTelemetry.

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

`UploadStatus` shows an `UploadOperationState`. It always renders a `section` named by `Label` and an initially empty `role="status"` element (an implicit polite live region), so the first announcement is read. The status holds the state's announcement (the state's ten-percent steps throttle what assistive technology reads) and, after a failure, the failure reason. It also renders a determinate `progress` only while uploading and an indeterminate one while queued, retrying, or processing, and a cancel button named after the file. When the cancel button had focus and disappears, focus moves to the section; focus changes on the button cause no render. An optional `Detail` fragment (given the state) renders caller content such as exact byte counts or a retry hint in a non-live `div data-upload-detail` after the status region; it is never announced, and the caller bounds its own text. The component renders when its parent re-renders after mutating the state, and it does not cancel transport work.

```razor
<UploadStatus State="_upload" Label="Customer import upload status" OnCancel="CancelUpload" />
```

File names and failure messages are encoded and truncated to 200 characters. The default English strings, `CancelText` and `ProgressLabel`, are parameters for localization; the announcement text comes from `UploadOperationState`.

To match existing accessible names and styling, all optional and unset by default (the markup is then unchanged):

| Parameter | Default | Purpose |
| --- | --- | --- |
| `CancelLabelFormat` | `null`: `{CancelText}: {file}` | Format for the cancel button's accessible name, `{0}` is the bounded file name; the visible text stays `CancelText`. |
| `ProgressLabelFormat` | `null`: `{ProgressLabel}: {file}` | Format for the progress accessible name, `{0}` is the bounded file name. |
| `IndeterminateProgressLabelFormat` | `null`: same as determinate | Separate progress name while the progress is indeterminate. |
| `ProgressTextFormat` | `null`: `{0} / {1}` | Format for the determinate progress fallback content, `{0}` transferred bytes, `{1}` total. |
| `CancelClass`, `ProgressClass`, `StatusClass` | `null`: no `class` attribute | CSS classes for the cancel button, the progress element, and the `role="status"` element (for example to hide it visually). |
| `RenderFailureInStatus` | `true` | When `false`, the failure message is not rendered in the live region; show it in `Detail`. |

Formats are composite format strings, so they work as plain Razor attributes (`CancelLabelFormat="Cancel upload of {0}"`) and need no reflection; a malformed format throws `FormatException` at render.

## Interactivity marker

`<InteractiveMarker />` renders a hidden element whose `data-interactive` attribute is `false` while a page is only prerendered and
`true` once the interactive renderer has taken over (`RendererInfo.IsInteractive`). Browser tests wait for
`InteractiveMarker.ReadySelector` instead of sleeping; `SharedKernel.Testing.Playwright` provides `WaitForBlazorInteractive`, which uses its
own copy of that selector (a test keeps the two equal) so the test package does not depend on this library.

## Server-backed forms

`ServerFormSubmission` keeps the state of a form that saves to a server: `IsSaving` (reset even when the save throws), the
field errors shown on the fields and in `ErrorSummary` (`Summary`), and a fixed general `Error` for failures that cannot be
shown on a field. `await submission.Submit(() => ...)` runs the save and returns `true` on success. A failed result that is
not a validation failure shows the constructor's `failureMessage`, except `ResultStatus.Conflict` and `ResultStatus.NotFound`,
where retrying cannot help: set the optional init properties `ConflictMessage` and `NotFoundMessage` (fixed, caller-authored
text; `null` falls back to `failureMessage`). A validation failure shows `Error` (the `failureMessage`) whenever the server errors left nothing to list in `Summary` or some could not
be placed, so a failed save is never silent. Server error text is never copied into `Error`.
`FormField<TValue>` renders a
label tied to its control and the field's `ValidationMessage`.

`Submit` is safe to wire to `EditForm` `OnSubmit`: a call made while a save is running returns `false` without running the save
(a double click cannot save twice); every accepted call first clears the previous `Summary` and `Error`, so a throwing save leaves
nothing stale; and it calls `EditContext.Validate()` first. If the context holds messages, such as the parse error an
`InputNumber` or `InputDate` reports for text it cannot read, the save does not run, `Submit` returns `false`, `IsSaving` stays
`false`, and `Summary` lists the messages of the known fields (`Error` stays `null`). Without this check the unparsable edit would
be dropped silently and the old value saved. Messages on fields that are not in `ServerValidationOptions.KnownFields` (or on the model) still block
the save but have no summary entry; when no message could be listed, `Error` shows the fixed failure message so the user is never
left with a silent no-op. List every field the form shows. Call `Submit` on the renderer's context (for example
inside the event handler), not from a background thread.

```csharp
_submission = new ServerFormSubmission(_context, options, field => "customer-" + field, "We could not save. Try again later.")
{
    ConflictMessage = "The data was changed or removed by someone else. Reload it and try again.",
    NotFoundMessage = "This record no longer exists.",
};
```

For a form that edits one kind of record, `ServerFormSubmission.ForRecord(context, knownFields, "customer", "customer")` builds the
same submission with control ids `customer-{field}` and default messages built from the noun ("This value is not valid.", "We could
not save the customer. Try again later.", "The customer was changed or removed by someone else. Reload the page and try again.",
"That customer no longer exists."). Each message can be replaced through the optional `invalidMessage`, `failureMessage`,
`conflictMessage` and `notFoundMessage` parameters.

```razor
<FormField Label="Email" InputId="customer-Email" For="() => _form.Email">
    <InputText id="customer-Email" @bind-Value="_form.Email" />
</FormField>
```

## Notes

- Link targets in `ErrorSummary` are fragment links on the current page; browsers move focus to a focusable target of a fragment navigation.
- Depends on the `Microsoft.AspNetCore.Components.Web` package and `PandaBear.SharedKernel.HttpClients`, not on the ASP.NET Core shared framework; see the [package decision](../../../docs/adr/20261027-blazor-helpers-package.md).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor`. C# namespace: `PandaBear.Blazor`. Assembly/project identity: `SharedKernel.Blazor`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Reusing a component when the parameter changes

Blazor keeps one component instance when the route changes between URLs that map to the same component, so state loaded in
`OnInitializedAsync` goes stale. Load in `OnParametersSetAsync` and ask a `ParameterTracker<T>` whether the value really changed:
`Update(value)` returns `Initial`, `Unchanged` or `Changed`. `ConfirmationState<TKey>` keeps the "confirm this row's delete" state of a list.

## Deleting a row from a list

`ListDelete<TKey>` is the whole delete flow of a list page: `Ask(key)` and `Cancel()` drive the confirmation, `IsPending(key)`
shows the confirm buttons, and `Error` holds the message to show. `Run(key, delete, reload, removeLocal)` leaves the confirmation,
sends the deletion and then follows the result: success or `ResultStatus.NotFound` calls `removeLocal(key)` so the page drops the
row without reading the list again; `ResultStatus.Conflict` calls `reload()` so the list shows what happened; any other failure only
sets `Error`. `Error` is the constructor's `failureMessage`, except for a conflict, which shows the failed result's detail (or the
optional `conflictMessage`). `delete` returns a `ValueTask<Result>`, so `Mediator.Send(...)` fits directly. A `Run` made while another is running only leaves the confirmation and sends nothing; disable the confirm buttons with `IsRunning` as in the sample. `IsRunning` is reset even when a callback throws. The conflict detail is application text: unlike `ServerFormSubmission.ConflictMessage` it is shown as is, so pass `conflictMessage` if it may come from a remote service.
The class keeps no rows and no `[PersistentState]`: the page keeps its own rows (loaded with `[PersistentState]` and `??=`) and passes
the callbacks.

```razor
<button type="button" disabled="@_delete.IsRunning" @onclick="() => _delete.Run(row.Id, () => Mediator.Send(new DeleteCustomer(row.Id)), Reload, Remove)">Confirm</button>
```

```csharp
private readonly ListDelete<Guid> _delete = new("We could not delete the customer. It may already be gone.");

private async Task Reload() => Customers = await Mediator.Send(new ListCustomers());

private void Remove(Guid id) => Customers = [.. Customers!.Where(row => row.Id != id)];
```

