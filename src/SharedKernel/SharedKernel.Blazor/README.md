# SharedKernel.Blazor

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Accessible Blazor helpers for HTTP outcome messages, server validation, error summaries, handled-failure reporting, upload status, bounded option search, and an image picker over a large library. The components are plain C# classes with no CSS or JavaScript requirement; style them with the `class` attribute they pass through.

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

`Submit` is safe to wire to `EditForm` `OnSubmit`: a call made while a save is running returns `false` without running the save
(a double click cannot save twice); every accepted call first clears the previous `Summary` and `Error`, so a throwing save leaves
nothing stale; and it calls `EditContext.Validate()` first. If the context holds messages, such as the parse error an
`InputNumber` or `InputDate` reports for text it cannot read, the save does not run, `Submit` returns `false`, `IsSaving` stays
`false`, and `Summary` lists the messages of the known fields (`Error` stays `null`). Without this check the unparsable edit would
be dropped silently and the old value saved. Messages on fields that are not in `ServerValidationOptions.KnownFields` (or on the model) still block
the save but have no summary entry; when no message could be listed, `Error` shows the fixed failure message so the user is never
left with a silent no-op. List every field the form shows. Call `Submit` on the renderer's context (for example
inside the event handler), not from a background thread. `ClearError()` hides a stale `Error` without a submit, for example when the
user acts in another part of the page and the form's old failure no longer applies; the field errors and `Summary` stay.

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

### Form fields

`FormField<TValue>` renders a label tied to its control and the messages of the bound field, and ties the messages to the control
for assistive technology (WCAG 1.3.1 and 3.3.1). Each message is a `div class="validation-message"` with a stable id,
`{InputId}-error` for the first and `{InputId}-error-2` and so on for more. The field hands the control its attributes as the child
content's context; spread them with `@attributes="context"`: `aria-describedby` lists the ids in `DescribedBy` (a help text, for
example) and then the message ids, and `aria-invalid="true"` is set while the field has a message. A valid field without
`DescribedBy` hands the control nothing. Pass the help id in `DescribedBy` rather than writing `aria-describedby` on the control,
which would replace the field's attribute. The field re-renders when the validation state changes and must be inside an `EditForm`.

```razor
<EditForm EditContext="_context" OnSubmit="Save" Context="form">
    <FormField Label="Email" InputId="customer-Email" For="() => _form.Email" DescribedBy="customer-Email-help">
        <InputText id="customer-Email" @bind-Value="_form.Email" @attributes="context" />
        <p id="customer-Email-help">We send the booking confirmation here.</p>
    </FormField>
</EditForm>
```

`EditForm` and `FormField` both name their child content's parameter `context`, so name one of them (`Context="form"` above);
otherwise Razor reports RZ9999.

## Named validation fields

Use `NamedFormField` when validation identifies the field by a key such as `en.sections[0].heading`.
Pass `editContext.Field(key)`, the input ID, a label, and child content that spreads the supplied ARIA
attributes. Set `Help` for an inline description or `Inline` for a label that follows a checkbox.
Leave `InputId` unset to render just the messages for a group. `EditContextErrorSummary.Build` accepts
an ordered sequence of field identifiers and focus target IDs for use with `ErrorSummary`.

## Text input with a typed value

Use `ParsedTextInputState<TValue>` to display edited text that may be temporarily invalid. Give it a value
formatter and a parser that returns the typed value and an optional validation message. Bind it to the
current `EditContext`, `FieldIdentifier` and value on each parameter update. Display its `Text` and send a
changed value back to the model only when `TrySetText` returns true.

The form's validation request rechecks the text and attaches the parser's message to the field. Rerenders
with the same accepted value keep the text, even if the bound field moves. A different model value resets
the text. Dispose the state with its owning component to detach validation and clear its messages. The
caller supplies number styles, scale, optionality, messages and the rendered control.

## Bounded lists

An Interactive Server page must not render or hold an unbounded list: every row costs server memory for the whole circuit,
and rows loaded with `[PersistentState]` are also written into the prerendered page and sent back in the message that starts
the circuit. SignalR rejects an incoming message above `HubOptions.MaximumReceiveMessageSize` (32 KB by default); the
connection then closes ("Server returned an error on close") and the page never becomes interactive. Microsoft's guidance
is to reduce the prerendered state rather than raise the limit, which increases the risk of denial of service
([Prerendered state size and SignalR message size limit](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-11.0#prerendered-state-size-and-signalr-message-size-limit)),
and to paginate, show only the first N items and require search criteria for the rest, or virtualize
([Resource exhaustion](https://learn.microsoft.com/aspnet/core/blazor/security/interactive-server-side-rendering?view=aspnetcore-11.0#resource-exhaustion)).
Choose by the kind of list:

- A list page (a table of records): page it through the URL
  ([State management using the URL](https://learn.microsoft.com/aspnet/core/blazor/state-management/?view=aspnetcore-11.0#state-management-using-the-url))
  with `PageWindow` (`PandaBear.SharedKernel.BuildingBlocks`) and the `Pager` component (`PandaBear.SharedKernel.Blazor.Site`), which
  renders plain links and works in static and interactive pages alike. One page of rows is bounded by the page size, so it may go
  into `[PersistentState]` (`ListSlice<TItem>` serializes with System.Text.Json); the whole list never may.
- A picker over a large option set (an image library, a customer lookup): search with `OptionSearch<TOption>`, which shows the
  first `Limit` matches and asks for search text for the rest; for images, `ImagePicker<TKey, TOption>` builds the whole picker on it.
- A small, fixed list (countries, statuses): neither; render it whole.

Both read a `ListSlice<TItem>`: at most the items asked for plus the total number of matches. The application's query does the
`Skip`/`Take` and the count, ordered by a unique key; the page keeps only the slice. To check a page's prerendered state in tests,
see `PrerenderedComponentState` in `PandaBear.SharedKernel.Testing.AspNetCore`.

### Searching a large option set

`OptionSearch<TOption>` holds a picker's search: the trimmed `Query` (cut to `MaxQueryLength`, 200), at most `Limit` `Results`,
the `Total` number of matches, `IsTruncated` (more match than shown), `IsSearching`, `HasResults` (the results belong to the
current query) and `IsQueryTooShort`. Give it the application's search, `(query, take, cancellationToken) => Task<ListSlice<TOption>>`,
which receives the trimmed text (empty when the user typed nothing, so the application decides which options to offer first, such
as the newest) and `take` equal to `Limit`. A result with more than `Limit` items is cut to `Limit`. Set with `init`:

- `Debounce` (default 300 ms, `DefaultDebounceMilliseconds`): `Search` waits this long and a newer `Search` during the wait replaces
  it, so a burst of keystrokes, each a round trip on Interactive Server, sends one query. `TimeSpan.Zero` searches at once.
  `TimeProvider` sets the clock of the wait, for tests.
- `MinQueryLength` (default 0): a shorter query fetches nothing and empties the results; `IsQueryTooShort` tells the page to ask
  for more text. With the default, the empty query asks for the first options.

Nothing is fetched while prerendering: until `Start()` a `Search` only records the query. Call `Start()` from
`OnAfterRenderAsync` when `firstRender` is true, which never runs while prerendering; it searches the current query at once. So the
prerendered page carries no options and nothing is fetched twice; render the selected value from the record itself.

Only the latest search applies: a new `Search` cancels the previous search's token, and a superseded result is dropped even
when it arrives later. A failure is not swallowed: like `ListDelete.Run`, `Search` and `Start` let the exception propagate (handle
it, or let `RecoveryErrorBoundary` report it), and the results of the failed query are emptied. Only the cancellation of a
superseded search completes quietly; a cancellation through the caller's token propagates. `Dispose` cancels the running search;
dispose the state with the component.

`Status(shownFormat, noMatchesFormat)` builds the announcement from your copy for one polite live region, `role="status"` with
`aria-atomic="true"`, never around the results list, which a screen reader would read as one flattened string
([Scott O'Hara, "dynamic results"](https://www.scottohara.me/blog/2022/02/05/dynamic-results.html)). `shownFormat` is a composite
format with `{0}` the number shown, `{1}` the total (current culture) and `{2}` the query; `noMatchesFormat` has `{0}` the query.
It returns `null` until a search for the current query has completed. Screen readers stay silent when a live region's text does
not change, so the text of consecutive searches always differs by a trailing no-break space that is not read; a search that gives
the same counts is announced again. `AnnouncementKey` changes with every completed search, for content you want to re-render (as
an `@key`). Render the region always, empty at first, so the first announcement is read.

```razor
@implements IDisposable

<label for="image-search">Search images</label>
<input id="image-search" type="search" value="@_images.Query" disabled="@(!RendererInfo.IsInteractive)"
       @oninput="e => _images.Search(e.Value?.ToString())" />
<p role="status" aria-atomic="true">@_images.Status("{0} of {1} images for ‘{2}’", "No images for ‘{0}’")</p>
@foreach (var image in _images.Results)
{
    <label><input type="radio" name="image" value="@image.Id" checked="@(image.Id == SelectedId)"
                  @onchange="() => Select(image.Id)" /> @image.Name</label>
}
@if (_images.IsQueryTooShort)
{
    <p>Type at least two letters.</p>
}
else if (_images.IsTruncated)
{
    <p>Type more of the name to find other images.</p>
}

@code {
    private OptionSearch<ImageOption> _images = default!;

    protected override void OnInitialized() =>
        _images = new OptionSearch<ImageOption>((query, take, token) => Images.Search(query, take, token), limit: 20) { MinQueryLength = 2 };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _images.Start();
            StateHasChanged();
        }
    }

    public void Dispose() => _images.Dispose();
}
```

Call `Search` and `Start` on the renderer's synchronization context (an event handler or a lifecycle method), as with
`ListDelete`. `OnAfterRenderAsync` does not render again by itself, hence the `StateHasChanged()` after `Start`.

### Picking images from a large library

`ImagePicker<TKey, TOption>` picks one image (single mode: `Value` and `ValueChanged`, or `@bind-Value`) or an ordered list (multiple
mode: `Values` and `ValuesChanged`, or `@bind-Values`) from a library too large to render or hold. Setting a parameter of each mode
throws `InvalidOperationException`; so do `MaxCount` and `Reorderable` in single mode. The picker never changes `Values`: it raises
`ValuesChanged` with a new list. `TKey` is a struct key such as a `Guid`; `TOption` is a small class with the key, a label and a
thumbnail address, never the image itself.

| Parameter | Meaning |
| --- | --- |
| `Id`, `Label` | The `fieldset`'s id (no white space; its controls' ids start with it, so an error summary can link to it) and its `legend`. Required. |
| `Help`, `Messages` | A help text and the field's validation messages, under the legend and tied to the group with `aria-describedby` (`{Id}-help`, `{Id}-error`, `{Id}-error-2`, …). |
| `Texts` | `ImagePickerTexts`, every word of the picker, English by default; replace any of it (`Texts="_texts with { Find = "Encontrar" }"`). Formats are composite formats; each property names its placeholders. |
| `EmptyLibrary` | What the search panel shows when the library is empty, such as a link to upload an image. |
| `Search` | `(query, take, cancellationToken) => Task<ListSlice<TOption>>`, as for `OptionSearch`. Required. |
| `Lookup` | `(keys, cancellationToken) => Task<IReadOnlyList<TOption>>`: the options of chosen keys the catalog does not know, for a page other than the persisted first one. Each key is asked for once. |
| `Catalog` | `PickerCatalog<TKey, TOption>`: the options shown as chosen (`Known`, `Find`, `Remember`, `KeyOf`). Required. |
| `LabelOf`, `ThumbnailOf` | The label of an option (required), and its thumbnail address: only a root-relative (`/site-media/…`) or an `https` address is shown (`WebAssetUriSanitizer`), anything else shows no thumbnail. When adopting, note that a base-relative address such as `site-media/…`, which a page with `<base href="/">` would resolve, is dropped: give the picker the address with its leading `/`. |
| `MaxCount`, `Reorderable` | Multiple mode: the most images, and move-earlier and move-later buttons on every chosen image. |
| `SearchLimit`, `ChosenPageSize` | 24 each (`ImagePicker.DefaultSearchLimit`, `ImagePicker.DefaultChosenPageSize`). |
| `MinQueryLength`, `Debounce`, `TimeProvider` | 0, 300 ms and `TimeProvider.System`, as on `OptionSearch`; `TimeProvider` is the clock of the debounce, for tests. The bounds, the debounce and its clock are read when the search first opens. |

The state stays small: the picker never holds the library, searches nothing until the find button opened the search on the
interactive page, and lists the chosen images `ChosenPageSize` at a time; other pages are looked up when they are shown. Persist only
the options of the first chosen page in the page's prerendered state, and build the catalog from them:

```razor
<ImagePicker TKey="Guid" TOption="ImageOption" Id="gallery-photos" Label="Photos" @bind-Values="_form.PhotoIds" Reorderable="true"
             Search="Images.Search" Lookup="Images.Of" Catalog="_catalog" LabelOf="image => image.Label"
             ThumbnailOf="image => image.ThumbnailUrl" Messages="_errors.For(nameof(_form.PhotoIds))" Texts="_pickerTexts" />

@code {
    [PersistentState]
    public List<ImageOption>? KnownImages { get; set; }

    private PickerCatalog<Guid, ImageOption> _catalog = default!;

    protected override async Task OnInitializedAsync()
    {
        _form = await Albums.Edit(Id);
        KnownImages ??= [.. await Images.Of(ImagePicker.FirstChosenPage(_form.PhotoIds), CancellationToken.None)];
        _catalog = new PickerCatalog<Guid, ImageOption>(image => image.Id, KnownImages);
    }
}
```

A test of this package prerenders a page with 1000 chosen images and checks with `PrerenderedComponentState` that it persists
exactly as much state as a page with 24.

Accessibility:

- a `fieldset` named by its `legend`, with the help and the messages tied by `aria-describedby`;
- one polite, atomic status region (`{Id}-status`, `role="status"`, `aria-atomic="true"`), rendered from the start, that announces the
  search results (through `OptionSearch.Status`), a full list, a move ("Photo 3 moved to position 1 of 2.") and the range of a newly
  shown page; the range of the page shown is also visible in `{Id}-range`, outside the live region;
- the find button (`{Id}-find`) has `aria-expanded` and `aria-controls` (`{Id}-panel`), and is named "{text}, {Label}";
- a result's button is named "Add {label}" ("Choose {label}" in single mode), or "{label}, chosen" with `aria-disabled` when it is chosen
  already; when `MaxCount` is reached every add button has `aria-disabled` and a click announces why;
- every chosen image has a "Remove {label}" button and, when reorderable, "Move {label} earlier" and "Move {label} later" (with
  `aria-disabled` at either end, so they keep focus);
- thumbnails are decorative (`alt=""`), lazy and 64 by 48 pixels;
- every control is `disabled` until the page is interactive, because a click on a prerendered page is lost;
- focus moves to the find button after a single choice and after the last image is removed, otherwise to the nearest remove button;
  after a move it stays on the moved image's button;
- the chosen list is an `ol` whose `start` numbers a later page, with previous and next buttons named "{text}, {Label}".

Every change applies to the latest list. A removal or a move that waits for the options of the page it will show reads the list again
when they arrive, so a change made meanwhile (another removal, an added image) is kept, and the picker shows the list it raised until
the parent passes a new one. Only the latest search of an open panel is announced: a search that a newer one or a close replaced ends
silently. Disposing the picker cancels the search and the lookup it waits for.

A chosen key without an option (an image deleted since) is listed as `ImagePickerTexts.Missing`. The picker uses the stable classes
`image-picker`, `image-picker__empty`, `image-picker__chosen`, `image-picker__pages`, `image-picker__panel` and
`image-picker__results`, and passes other attributes, such as `class`, to the `fieldset`.

## Notes

- Link targets in `ErrorSummary` are fragment links on the current page; browsers move focus to a focusable target of a fragment navigation.
- Depends on the `Microsoft.AspNetCore.Components.Web` package, `PandaBear.SharedKernel.HttpClients` and, for the image picker's `PageWindow` and thumbnail check, `PandaBear.SharedKernel.BuildingBlocks`, not on the ASP.NET Core shared framework; see the [package decision](../../../docs/adr/20261027-blazor-helpers-package.md).

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor`. C# namespace: `PandaBear.Blazor`. Assembly/project identity: `SharedKernel.Blazor`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Reusing a component when the parameter changes

Blazor keeps one component instance when the route changes between URLs that map to the same component, so state loaded in
`OnInitializedAsync` goes stale. Load in `OnParametersSetAsync` and ask a `ParameterTracker<T>` whether the value really changed:
`Update(value)` returns `Initial`, `Unchanged` or `Changed`. `ConfirmationState<TKey>` keeps the "confirm this row's delete" state of a list.

## Keeping focus on an inline confirmation

When a button is replaced by a confirmation, the focused element leaves the page and keyboard focus falls to the document.
`ConfirmationState<TKey>` (and `ListDelete<TKey>`, which uses it) keeps focus where the user is. Capture two elements with `@ref`:
the control that opens each row's confirmation in `Openers[key]`, and the first harmless control inside the open confirmation
(its Cancel button, or an optional reason field; never the confirming button) in `SafeControl`. Then call `MoveFocus()` from
`OnAfterRenderAsync`:

- `Ask(key)` moves focus to `SafeControl`.
- `Cancel()` closes the confirmation and moves focus back to the opener of the row that was waiting.
- `Close()` closes it without moving focus; use it after acting on the confirmation or when the page resets, where the opener may
  not come back.

Focus moves once per request, after the render that captured the element; an element that is not captured is not focused, so the
focus handling is opt-in. `FocusHelper.TryFocus(element)` is the focus call underneath, public for a page's own focus moves: it does
nothing for a reference no render captured, a disconnected circuit, a disposed renderer or JavaScript runtime, or an element a later
render removed, so a focus move never fails the page. It uses `ElementReference.FocusAsync`, and the package ships no script.

## Deleting a row from a list

`ListDelete<TKey>` is the whole delete flow of a list page: `Ask(key)` and `Cancel()` drive the confirmation, `IsPending(key)`
shows the confirm buttons, and `Error` holds the message to show. `Run(key, delete, reload, removeLocal)` leaves the confirmation,
sends the deletion and then follows the result: success or `ResultStatus.NotFound` calls `removeLocal(key)` so the page drops the
row without reading the list again; `ResultStatus.Conflict` calls `reload()` so the list shows what happened; any other failure only
sets `Error`. `Error` is the constructor's `failureMessage`, except for a conflict, which shows the failed result's detail (or the
optional `conflictMessage`). `delete` returns a `ValueTask<Result>`, so `Mediator.Send(...)` fits directly. A `Run` made while another is running only leaves the confirmation and sends nothing; disable the confirm buttons with `IsRunning` as in the sample. `IsRunning` is reset even when a callback throws. The conflict detail is application text: unlike `ServerFormSubmission.ConflictMessage` it is shown as is, so pass `conflictMessage` if it may come from a remote service.
The class keeps no rows and no `[PersistentState]`: the page keeps its own rows (loaded with `[PersistentState]` and `??=`) and passes
the callbacks.

Focus follows the confirmation as described in [Keeping focus on an inline confirmation](#keeping-focus-on-an-inline-confirmation).
After `Run`, a deletion that succeeded shows the optional `deletedStatus` text (caller-authored, for example "Ana Souza was
deleted.") in `Status` and moves focus to `StatusRegion`, because the row and its buttons are gone; render `Status` in a polite status
region (`role="status"`, `tabindex="-1"`). A conflict or another failure moves focus back to the row's opener while the page shows
`Error`; a row that was already gone moves no focus. A deletion that ends while the user has opened another row's confirmation
leaves focus in that confirmation. The next `Ask` or `Run` clears `Status`, and a deleted row's opener is forgotten.

```razor
@if (_delete.Status is not null)
{
    <p role="status" tabindex="-1" @ref="_delete.StatusRegion">@_delete.Status</p>
}
@foreach (var row in Customers)
{
    @if (_delete.IsPending(row.Id))
    {
        <button type="button" disabled="@_delete.IsRunning" @onclick="() => Delete(row)">Confirm delete @row.Name</button>
        <button type="button" @ref="_delete.SafeControl" @onclick="_delete.Cancel">Cancel</button>
    }
    else
    {
        <button type="button" @ref="_delete.Openers[row.Id]" @onclick="() => _delete.Ask(row.Id)">Delete @row.Name</button>
    }
}
```

```csharp
private readonly ListDelete<Guid> _delete = new("We could not delete the customer. It may already be gone.");

private Task Delete(CustomerListItem row) =>
    _delete.Run(row.Id, () => Mediator.Send(new DeleteCustomer(row.Id)), Reload, Remove, $"{row.Name} was deleted.");

private async Task Reload() => Customers = await Mediator.Send(new ListCustomers());

private void Remove(Guid id) => Customers = [.. Customers!.Where(row => row.Id != id)];

protected override Task OnAfterRenderAsync(bool firstRender) => _delete.MoveFocus();
```


## Creating or editing a record

`EditSession<TKey, TForm, TRecord>` is the lifecycle of a page that creates a record (no key) or edits one (a key). The constructor
takes the known fields, the id prefix and the noun of `ServerFormSubmission.ForRecord`, and an optional `conflictMessage` that replaces
the default conflict text for every key. Call
`Sync(key, load, fill, onReset)` from `OnParametersSetAsync`: it returns at once while the key is unchanged; when the key changed it drops
the previous form, record and submission, runs the optional `onReset` for the page's own state, loads the record of an existing key
(`load` returns a `Result<TRecord>`; a failure sets `IsMissing`), copies it into the form with `fill`, and makes `Context` (the
`EditContext`) and `Submission` (the `ServerFormSubmission`, built like `ServerFormSubmission.ForRecord`) ready. A load that finishes after
the route moved on is dropped, so the older key never overwrites the newer one. The form is a new `TForm` for each key.

The page keeps what is its own: the form fields, the commands that save and the navigation after a save. `[PersistentState]` stays on the
page because it only works on a component property; pass the property through to `Record`, so the record restored from the prerendered page
is not read again (it is used by the first `Sync` instead of calling `load`).

```csharp
private readonly EditSession<Guid, CustomerForm, CustomerDto> _session = new(KnownFields, "customer", "customer");

[Parameter]
public Guid? Id { get; set; }

[PersistentState]
public CustomerDto? Existing { get => _session.Record; set => _session.Record = value; }

protected override Task OnParametersSetAsync() =>
    _session.Sync(Id, id => Mediator.Send(new GetCustomer(id)), (form, customer) => form.FirstName = customer.FirstName);

private async Task Save()
{
    var saved = await _session.Submission!.Submit(async () => Id is { } id
        ? await Mediator.Send(new UpdateCustomer(id, _session.Form.ToDefinition(), _session.Record!.Version))
        : (await Mediator.Send(new CreateCustomer(_session.Form.ToDefinition()))).WithoutValue());
    if (saved)
    {
        Navigation.NavigateTo("customers");
    }
}

public void Dispose() => _session.Dispose();
```

In the markup, show the "no longer exists" message when `_session.IsMissing`, and otherwise `<EditForm EditContext="_session.Context">` with
`_session.Submission.Summary`, `.Error` and `.IsSaving` as for any `ServerFormSubmission`. Page state that must be cleared when the key changes
(a confirmation, a notice) goes in `onReset`.

`load` returns a `ValueTask<Result<TRecord>>`, so `Mediator.Send(...)` fits directly.

Lookup lists the form chooses from (tours, customers) do not depend on the key, so they are not part of the session: load them once in
`OnInitializedAsync` with `[PersistentState]` and `??=`. Blazor finishes `OnInitializedAsync` before the first `OnParametersSetAsync`, so the
lists are there before the form exists, and a route change between two records does not reload them. A page that has a second, independent form
(for example a notes form) builds it after `Sync` returns, once `Context` is not `null`, and drops it in `onReset`; guard that code with the
page's own state (the second context is `null`), because a `Sync` that was superseded by a newer key also returns.
