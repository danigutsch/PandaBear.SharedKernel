# SharedKernel.Blazor

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Accessible Blazor helpers for HTTP outcome messages, server validation, error summaries, handled-failure reporting, and upload status. The components are plain C# classes with no CSS or JavaScript requirement; style them with the `class` attribute they pass through.

## HTTP failures and server validation

Show server failures in two tiers and never join server text into one message.

1. Field-keyed validation errors go to the form through `ServerValidationBinder`, which uses a `ValidationMessageStore`. Only fields named in `ServerValidationOptions.KnownFields` are shown (matched without regard to case). Names and messages are bounded; blank, multi-line, or stack-trace-like messages become the fallback field message. A field's messages are cleared when that field changes. Dispose the binder with the form.
2. Everything else shows fixed copy that you choose. `HttpOutcomeMessages` maps an `HttpOutcomeKind` to your copy and never reads `HttpOutcome.Message` or `ValidationErrors`. `ServerValidationResult.HasUnplacedErrors` tells you when a validation response had keys that could not be placed.

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

`ErrorSummary` renders a labelled `section` (`tabindex="-1"`) with a heading and fragment links to the fields, and takes focus when new items appear. Give each field a stable `id`, and show a message beside each field with `ValidationMessage`.

```razor
<ErrorSummary Title="There is a problem" Items="_summary" class="error-summary" />
```

## Handled UI failures

Blazor Server component exceptions do not reach the ASP.NET Core exception handler, and code that catches them often shows a message and nothing else. `IUiFailureReporter.Report(exception, operation)` logs `Handled UI failure in {Operation}: {ExceptionType} (trace {TraceId})` under `EventId` 7001 (`HandledUiFailure`). The exception object, message, stack, and data are never logged. Operation names must be code-chosen constants; unsafe or overlong names are logged as `unknown`.

`IHandledFailureCounter` is the seam for the `unhandled_exceptions_total` counter with the tag `handled=true`, so handled and unhandled failures share one series. This package defines only the interface; an application, or `SharedKernel.AspNetCore` once it provides one, supplies the implementation. Without one, failures are logged and not counted.

```csharp
builder.Services.AddUiFailureReporting();
builder.Services.AddSingleton<IHandledFailureCounter, MyHandledFailureCounter>(); // optional
```

```razor
<RecoveryErrorBoundary RecoveryMessage="Something went wrong. Reload the page to continue."
                       RecoverLabel="Try again" OperationName="app.root">
    @Body
</RecoveryErrorBoundary>
```

`RecoveryErrorBoundary` reports the fault, renders only your fixed copy in a `role="alert"` element, and offers a button to render the child again when you supply `RecoverLabel`.

## Upload status

`UploadStatus` shows an `UploadOperationState`. It renders a `section` named by `Label`, a polite, atomic `role="status"` element with the state's announcement (the state's ten-percent steps throttle what assistive technology reads), a determinate `progress` only while uploading and an indeterminate one while queued, retrying, or processing, and a cancel button named after the file. When the cancel button had focus and disappears, focus moves to the section. The component renders when its parent re-renders after mutating the state, and it does not cancel transport work.

```razor
<UploadStatus State="_upload" Label="Customer import upload status" OnCancel="CancelUpload" />
```

File names and failure messages are encoded and truncated to 200 characters. The default English strings, `CancelText` and `ProgressLabel`, are parameters for localization; the announcement text comes from `UploadOperationState`.

## Notes

- Link targets in `ErrorSummary` are fragment links (`#id`); browsers move focus to a focusable target of a fragment navigation.
- Depends on the ASP.NET Core shared framework and `PandaBear.SharedKernel.HttpClients`; see the [package decision](../../../docs/adr/20261027-blazor-helpers-package.md).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor`. C# namespace: `PandaBear.Blazor`. Assembly/project identity: `SharedKernel.Blazor`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
