using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using PandaBear.BuildingBlocks;
using PandaBear.InputNormalization;

namespace PandaBear.Blazor;

/// <summary>
/// Picks one image (single mode: <see cref="Value"/>) or an ordered list of images (multiple mode: <see cref="Values"/>) from a library
/// too large to render or to hold: the chosen images come from the page's <see cref="Catalog"/> and are listed
/// <see cref="ChosenPageSize"/> at a time, and the library is only searched, at most <see cref="SearchLimit"/> options at a time, after
/// the find button opened the search, which needs the interactive page.
/// </summary>
/// <typeparam name="TKey">The key of an option, such as an image id.</typeparam>
/// <typeparam name="TOption">The option, typically a small projection with the key, a label and a thumbnail address.</typeparam>
/// <remarks>
/// <para>
/// The picker is a <c>fieldset</c> named by its <c>legend</c>, with the help and the field's messages tied to it by
/// <c>aria-describedby</c>. One polite, atomic status region, rendered from the start, announces the search results, a full list, a move
/// and the range of a newly shown page of chosen images. The find button tells whether the search is open (<c>aria-expanded</c>) and
/// which panel it opens (<c>aria-controls</c>). A result's button is named "Add {label}" (or "Choose {label}" in single mode), or
/// "{label}, chosen" with <c>aria-disabled</c> when it is chosen already; a chosen image has a "Remove {label}" button and, when
/// <see cref="Reorderable"/>, move-earlier and move-later buttons. Thumbnails are decorative (<c>alt=""</c>), lazy and 64 by 48 pixels.
/// Every control is disabled until the page is interactive, because a click on a prerendered page is lost. After a single choice, or
/// after the last image is removed, focus moves to the find button; after another removal it moves to the nearest remove button, and
/// after a move it stays on the moved image's button. The chosen list is an <c>ol</c> whose <c>start</c> numbers a later page.
/// </para>
/// <para>
/// The picker never holds the library. Nothing is searched while prerendering. Persist only the options of
/// <see cref="ImagePicker.FirstChosenPage{TKey}"/> in the page's prerendered state and build the <see cref="Catalog"/> from them; the
/// options of another page are read with <see cref="Lookup"/> when it is shown. The bounds and the debounce are read when the search
/// first opens.
/// </para>
/// </remarks>
public sealed class ImagePicker<TKey, TOption> : ComponentBase, IDisposable
    where TKey : struct, IEquatable<TKey>
    where TOption : class
{
    private const int MaxLabelLength = 200;
    private const int MaxMessageLength = 300;
    private const int ThumbnailWidth = 64;
    private const int ThumbnailHeight = 48;
    private const char AnnouncementToggle = ' ';

    private readonly Dictionary<TKey, ElementReference> _removeButtons = [];
    private readonly Dictionary<TKey, ElementReference> _earlierButtons = [];
    private readonly Dictionary<TKey, ElementReference> _laterButtons = [];
    private readonly HashSet<TKey> _requested = [];
    private readonly CancellationTokenSource _disposal = new();
    private PickerCatalog<TKey, TOption>? _catalog;
    private OptionSearch<TOption>? _search;
    private ElementReference _toggle;
    private int _chosenPage = 1;
    private bool _open;
    private string? _announcement;
    private int _announcements;
    private FocusTarget _focus;
    private TKey _focusKey;
    private bool _disposed;
    private IReadOnlyList<TKey>? _receivedValues;
    private IReadOnlyList<TKey>? _raisedValues;
    private int _searchGeneration;

    private enum FocusTarget
    {
        None,
        Toggle,
        Remove,
        Earlier,
        Later,
    }

    /// <summary>Gets or sets the id of the <c>fieldset</c>, which an error summary links to; the ids of its controls start with it.</summary>
    [Parameter]
    [EditorRequired]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the visible name of the picker, its <c>legend</c>.</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional help text under the legend.</summary>
    [Parameter]
    public string? Help { get; set; }

    /// <summary>Gets or sets the field's validation messages, shown under the help and tied to the group.</summary>
    [Parameter]
    public IReadOnlyList<string>? Messages { get; set; }

    /// <summary>Gets or sets the copy; English by default.</summary>
    [Parameter]
    public ImagePickerTexts Texts { get; set; } = new();

    /// <summary>
    /// Gets or sets what the search panel shows when the library is empty, such as a link to upload an image; <see langword="null"/>
    /// shows nothing more than the announcement.
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyLibrary { get; set; }

    /// <summary>
    /// Gets or sets the application's search: the options that match a trimmed query (empty for the options to offer first, such as the
    /// newest), at most the given number, and the number of all matches. See <see cref="OptionSearch{TOption}"/>.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<string, int, CancellationToken, Task<ListSlice<TOption>>> Search { get; set; } = null!;

    /// <summary>
    /// Gets or sets how the options of chosen keys the <see cref="Catalog"/> does not know are read, for a page of chosen images other
    /// than the persisted first one. A key without an option is shown with <see cref="ImagePickerTexts.Missing"/>. Each key is asked for
    /// at most once. <see langword="null"/> reads nothing.
    /// </summary>
    [Parameter]
    public Func<IReadOnlyList<TKey>, CancellationToken, Task<IReadOnlyList<TOption>>>? Lookup { get; set; }

    /// <summary>Gets or sets the options of the chosen images; an option looked up or chosen from a search is remembered in it.</summary>
    [Parameter]
    [EditorRequired]
    public PickerCatalog<TKey, TOption> Catalog { get; set; } = null!;

    /// <summary>Gets or sets the label of an option, shown beside its thumbnail and used in the buttons' names.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TOption, string> LabelOf { get; set; } = null!;

    /// <summary>
    /// Gets or sets the thumbnail address of an option. Only a root-relative or an https address is shown; anything else, or
    /// <see langword="null"/>, shows no thumbnail. <see langword="null"/> shows none at all.
    /// </summary>
    [Parameter]
    public Func<TOption, string?>? ThumbnailOf { get; set; }

    /// <summary>Gets or sets the chosen key in single mode.</summary>
    [Parameter]
    public TKey? Value { get; set; }

    /// <summary>Gets or sets the callback of a changed choice in single mode.</summary>
    [Parameter]
    public EventCallback<TKey?> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets the chosen keys in multiple mode, in order. The picker never changes the list: it raises <see cref="ValuesChanged"/>
    /// with a new one. Setting it, or <see cref="ValuesChanged"/>, together with a single-mode parameter throws.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TKey>? Values { get; set; }

    /// <summary>Gets or sets the callback of a changed list in multiple mode.</summary>
    [Parameter]
    public EventCallback<IReadOnlyList<TKey>> ValuesChanged { get; set; }

    /// <summary>Gets or sets the most keys that can be chosen in multiple mode; <see langword="null"/> for no limit.</summary>
    [Parameter]
    public int? MaxCount { get; set; }

    /// <summary>Gets or sets whether a chosen image has buttons that move it one place earlier or later, in multiple mode.</summary>
    [Parameter]
    public bool Reorderable { get; set; }

    /// <summary>Gets or sets the most options one search shows; <see cref="ImagePicker.DefaultSearchLimit"/> by default.</summary>
    [Parameter]
    public int SearchLimit { get; set; } = ImagePicker.DefaultSearchLimit;

    /// <summary>Gets or sets the most chosen images listed at a time; <see cref="ImagePicker.DefaultChosenPageSize"/> by default.</summary>
    [Parameter]
    public int ChosenPageSize { get; set; } = ImagePicker.DefaultChosenPageSize;

    /// <summary>Gets or sets the shortest query that is searched, as <see cref="OptionSearch{TOption}.MinQueryLength"/>; 0 by default.</summary>
    [Parameter]
    public int MinQueryLength { get; set; }

    /// <summary>Gets or sets how long typing waits before it searches, as <see cref="OptionSearch{TOption}.Debounce"/>; 300 ms by default.</summary>
    [Parameter]
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(OptionSearch<TOption>.DefaultDebounceMilliseconds);

    /// <summary>Gets or sets additional attributes applied to the <c>fieldset</c>, such as <c>class</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsMultiple => Values is not null || ValuesChanged.HasDelegate;

    private IReadOnlyList<TKey> Chosen => _raisedValues ?? Values ?? (Value is { } value ? [value] : []);

    private PageWindow ChosenWindow => PageWindow.Create(_chosenPage, ChosenPageSize, Chosen.Count);

    private bool IsFull => IsMultiple && MaxCount is { } most && Chosen.Count >= most;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _search?.Dispose();
        _disposal.Cancel();
        _disposal.Dispose();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><see cref="Id"/> is blank or holds white space, <see cref="Label"/> is blank, or <see cref="Values"/> repeats a key.</exception>
    /// <exception cref="ArgumentNullException"><see cref="Search"/>, <see cref="Catalog"/>, <see cref="LabelOf"/> or <see cref="Texts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is zero or negative, or <see cref="MinQueryLength"/> or <see cref="Debounce"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Single-mode and multiple-mode parameters are both set, or <see cref="MaxCount"/> or <see cref="Reorderable"/> is set in single mode.</exception>
    protected override Task OnParametersSetAsync()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        if (Id.AsSpan().ContainsAnyWhiteSpace())
        {
            throw new ArgumentException("The id must not contain white space.", nameof(Id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentNullException.ThrowIfNull(Search);
        ArgumentNullException.ThrowIfNull(Catalog);
        ArgumentNullException.ThrowIfNull(LabelOf);
        ArgumentNullException.ThrowIfNull(Texts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SearchLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ChosenPageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(MinQueryLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MinQueryLength, OptionSearch<TOption>.MaxQueryLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(Debounce, TimeSpan.Zero);
        if (MaxCount is { } most)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(most, nameof(MaxCount));
        }

        if (IsMultiple && (Value is not null || ValueChanged.HasDelegate))
        {
            throw new InvalidOperationException("Set either Value and ValueChanged (single mode) or Values and ValuesChanged (multiple mode), not both.");
        }

        if (!IsMultiple && (MaxCount is not null || Reorderable))
        {
            throw new InvalidOperationException("MaxCount and Reorderable apply to multiple mode only; set Values and ValuesChanged.");
        }

        if (Values is { } values && values.Distinct().Count() != values.Count)
        {
            throw new ArgumentException("The chosen keys must not repeat.", nameof(Values));
        }

        if (!ReferenceEquals(_receivedValues, Values))
        {
            _receivedValues = Values;
            _raisedValues = null;
        }

        if (!ReferenceEquals(_catalog, Catalog))
        {
            _catalog = Catalog;
            _requested.Clear();
        }

        var window = ChosenWindow;
        _chosenPage = window.Page;
        return LoadOptions(Chosen.Skip(window.Skip).Take(window.Take));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var target = _focus;
        _focus = FocusTarget.None;
        var buttons = target switch
        {
            FocusTarget.Remove => _removeButtons,
            FocusTarget.Earlier => _earlierButtons,
            FocusTarget.Later => _laterButtons,
            _ => null,
        };
        if (target == FocusTarget.Toggle)
        {
            await FocusHelper.TryFocus(_toggle).ConfigureAwait(true);
        }
        else if (buttons is not null && buttons.TryGetValue(_focusKey, out var button))
        {
            await FocusHelper.TryFocus(button).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var messages = Messages?.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        var description = string.Join(' ', Enumerable.Range(0, messages.Length).Select(MessageId).Prepend(Help is null ? null : Id + "-help").OfType<string>());

        builder.OpenElement(0, "fieldset");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "id", Id);
        builder.AddAttribute(3, "class", "image-picker");
        builder.AddAttribute(4, "tabindex", "-1");
        builder.AddAttribute(5, "aria-describedby", description.Length == 0 ? null : description);
        builder.OpenElement(6, "legend");
        builder.AddContent(7, Label);
        builder.CloseElement();
        if (Help is not null)
        {
            builder.OpenElement(8, "p");
            builder.AddAttribute(9, "id", Id + "-help");
            builder.AddAttribute(10, "class", "help");
            builder.AddContent(11, Help);
            builder.CloseElement();
        }

        for (var index = 0; index < messages.Length; index++)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "id", MessageId(index));
            builder.AddAttribute(14, "class", "validation-message");
            builder.AddContent(15, BoundedText.ToDisplay(messages[index], MaxMessageLength));
            builder.CloseElement();
        }

        builder.OpenRegion(16);
        BuildChosen(builder);
        builder.CloseRegion();
        builder.OpenElement(17, "p");
        builder.OpenElement(18, "button");
        builder.AddAttribute(19, "type", "button");
        builder.AddAttribute(20, "id", Id + "-find");
        builder.AddAttribute(21, "aria-expanded", _open ? "true" : "false");
        builder.AddAttribute(22, "aria-controls", Id + "-panel");
        builder.AddAttribute(23, "aria-label", Format(Texts.ToggleFormat, ToggleText, Label));
        builder.AddAttribute(24, "disabled", !RendererInfo.IsInteractive);
        builder.AddAttribute(25, "onclick", EventCallback.Factory.Create(this, Toggle));
        builder.AddElementReferenceCapture(26, element => _toggle = element);
        builder.AddContent(27, ToggleText);
        builder.CloseElement();
        builder.CloseElement();
        builder.OpenElement(28, "p");
        builder.AddAttribute(29, "id", Id + "-status");
        builder.AddAttribute(30, "role", "status");
        builder.AddAttribute(31, "aria-atomic", "true");
        builder.AddContent(32, _announcement);
        builder.CloseElement();
        if (_open && _search is not null)
        {
            builder.OpenRegion(33);
            BuildPanel(builder, _search);
            builder.CloseRegion();
        }

        builder.CloseElement();
    }

    private string ToggleText => _open ? Texts.Close : Texts.Find;

    private static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private string MessageId(int index) => index == 0 ? Id + "-error" : $"{Id}-error-{index + 1}";

    private string LabelFor(TOption? option) =>
        option is null ? Texts.Missing : BoundedText.ToDisplay(LabelOf(option), MaxLabelLength) is { Length: > 0 } label ? label : Texts.Missing;

    private string? ThumbnailFor(TOption option) =>
        ThumbnailOf is null ? null : WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(ThumbnailOf(option), WebUrl.MaxLength);

    private void BuildChosen(RenderTreeBuilder builder)
    {
        var chosen = Chosen;
        if (chosen.Count == 0)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "image-picker__empty");
            builder.AddContent(2, Texts.Empty);
            builder.CloseElement();
            return;
        }

        var window = ChosenWindow;
        var interactive = RendererInfo.IsInteractive;
        builder.OpenElement(3, "ol");
        builder.AddAttribute(4, "class", "image-picker__chosen");
        builder.AddAttribute(5, "start", window.Skip > 0 ? (window.Skip + 1).ToString(CultureInfo.InvariantCulture) : null);
        var last = Math.Min(window.Skip + window.Take, chosen.Count);
        for (var position = window.Skip; position < last; position++)
        {
            var key = chosen[position];
            var option = Catalog.Find(key);
            var label = LabelFor(option);
            var number = (position + 1).ToString(CultureInfo.InvariantCulture);
            builder.OpenElement(6, "li");
            builder.SetKey(key);
            AddThumbnail(builder, 7, option);
            builder.OpenElement(8, "span");
            builder.AddContent(9, label);
            builder.CloseElement();
            if (Reorderable)
            {
                builder.OpenElement(10, "button");
                builder.AddAttribute(11, "type", "button");
                builder.AddAttribute(12, "id", $"{Id}-earlier-{number}");
                builder.AddAttribute(13, "aria-label", Format(Texts.MoveEarlierFormat, label));
                builder.AddAttribute(14, "aria-disabled", position == 0 ? "true" : null);
                builder.AddAttribute(15, "disabled", !interactive);
                builder.AddAttribute(16, "onclick", EventCallback.Factory.Create(this, () => Move(key, -1)));
                builder.AddElementReferenceCapture(17, element => _earlierButtons[key] = element);
                builder.AddContent(18, Texts.MoveEarlier);
                builder.CloseElement();
                builder.OpenElement(19, "button");
                builder.AddAttribute(20, "type", "button");
                builder.AddAttribute(21, "id", $"{Id}-later-{number}");
                builder.AddAttribute(22, "aria-label", Format(Texts.MoveLaterFormat, label));
                builder.AddAttribute(23, "aria-disabled", position == chosen.Count - 1 ? "true" : null);
                builder.AddAttribute(24, "disabled", !interactive);
                builder.AddAttribute(25, "onclick", EventCallback.Factory.Create(this, () => Move(key, 1)));
                builder.AddElementReferenceCapture(26, element => _laterButtons[key] = element);
                builder.AddContent(27, Texts.MoveLater);
                builder.CloseElement();
            }

            builder.OpenElement(28, "button");
            builder.AddAttribute(29, "type", "button");
            builder.AddAttribute(30, "id", $"{Id}-remove-{number}");
            builder.AddAttribute(31, "aria-label", Format(Texts.RemoveFormat, label));
            builder.AddAttribute(32, "disabled", !interactive);
            builder.AddAttribute(33, "onclick", EventCallback.Factory.Create(this, () => Remove(key)));
            builder.AddElementReferenceCapture(34, element => _removeButtons[key] = element);
            builder.AddContent(35, Texts.Remove);
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
        if (!window.HasMultiplePages)
        {
            return;
        }

        builder.OpenElement(36, "p");
        builder.AddAttribute(37, "id", Id + "-range");
        builder.AddContent(38, RangeText(window));
        builder.CloseElement();
        builder.OpenElement(39, "p");
        builder.AddAttribute(40, "class", "image-picker__pages");
        AddPageButton(builder, 41, "-previous", Texts.Previous, window.PreviousPage, interactive);
        AddPageButton(builder, 42, "-next", Texts.Next, window.NextPage, interactive);
        builder.CloseElement();
    }

    private void AddPageButton(RenderTreeBuilder builder, int sequence, string suffix, string text, int? page, bool interactive)
    {
        builder.OpenRegion(sequence);
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "id", Id + suffix);
        builder.AddAttribute(3, "aria-label", Format(Texts.PageFormat, text, Label));
        builder.AddAttribute(4, "aria-disabled", page is null ? "true" : null);
        builder.AddAttribute(5, "disabled", !interactive);
        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create(this, () => ShowChosenPage(page)));
        builder.AddContent(7, text);
        builder.CloseElement();
        builder.CloseRegion();
    }

    private void AddThumbnail(RenderTreeBuilder builder, int sequence, TOption? option)
    {
        if (option is null || ThumbnailFor(option) is not { } address)
        {
            return;
        }

        builder.OpenRegion(sequence);
        builder.OpenElement(0, "img");
        builder.AddAttribute(1, "src", address);
        builder.AddAttribute(2, "alt", string.Empty);
        builder.AddAttribute(3, "width", ThumbnailWidth);
        builder.AddAttribute(4, "height", ThumbnailHeight);
        builder.AddAttribute(5, "loading", "lazy");
        builder.CloseElement();
        builder.CloseRegion();
    }

    private void BuildPanel(RenderTreeBuilder builder, OptionSearch<TOption> search)
    {
        var searchHelpId = Texts.SearchHelp is null ? null : Id + "-search-help";
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "id", Id + "-panel");
        builder.AddAttribute(2, "class", "image-picker__panel");
        builder.OpenElement(3, "label");
        builder.AddAttribute(4, "for", Id + "-search");
        builder.AddContent(5, Texts.SearchLabel);
        builder.CloseElement();
        if (searchHelpId is not null)
        {
            builder.OpenElement(6, "p");
            builder.AddAttribute(7, "id", searchHelpId);
            builder.AddAttribute(8, "class", "help");
            builder.AddContent(9, Texts.SearchHelp);
            builder.CloseElement();
        }

        builder.OpenElement(10, "input");
        builder.AddAttribute(11, "id", Id + "-search");
        builder.AddAttribute(12, "type", "search");
        builder.AddAttribute(13, "value", search.Query);
        builder.AddAttribute(14, "aria-describedby", searchHelpId);
        builder.AddAttribute(15, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, SearchFor));
        builder.CloseElement();
        if (search.IsQueryTooShort)
        {
            builder.OpenElement(16, "p");
            builder.AddAttribute(17, "class", "help");
            builder.AddContent(18, Format(Texts.QueryTooShortFormat, MinQueryLength));
            builder.CloseElement();
        }

        if (search.Results.Count > 0)
        {
            BuildResults(builder, search);
        }

        if (search.IsTruncated)
        {
            builder.OpenElement(40, "p");
            builder.AddAttribute(41, "class", "help");
            builder.AddContent(42, Format(Texts.TruncatedFormat, search.Results.Count));
            builder.CloseElement();
        }

        if (search is { HasResults: true, Total: 0, Query.Length: 0 })
        {
            builder.AddContent(43, EmptyLibrary);
        }

        builder.CloseElement();
    }

    private void BuildResults(RenderTreeBuilder builder, OptionSearch<TOption> search)
    {
        var chosen = Chosen;
        var full = IsFull;
        var multiple = IsMultiple;
        builder.OpenElement(19, "ul");
        builder.AddAttribute(20, "id", Id + "-results");
        builder.AddAttribute(21, "class", "image-picker__results");
        for (var index = 0; index < search.Results.Count; index++)
        {
            var option = search.Results[index];
            var key = Catalog.KeyOf(option);
            var label = LabelFor(option);
            var isChosen = chosen.Contains(key);
            builder.OpenElement(22, "li");
            builder.SetKey(key);
            AddThumbnail(builder, 23, option);
            builder.OpenElement(24, "span");
            builder.AddContent(25, label);
            builder.CloseElement();
            builder.OpenElement(26, "button");
            builder.AddAttribute(27, "type", "button");
            builder.AddAttribute(28, "id", $"{Id}-result-{(index + 1).ToString(CultureInfo.InvariantCulture)}");
            builder.AddAttribute(29, "aria-disabled", isChosen || full ? "true" : null);
            builder.AddAttribute(
                30,
                "aria-label",
                isChosen ? Format(Texts.ChosenFormat, label) : Format(multiple ? Texts.AddFormat : Texts.ChooseFormat, label));
            builder.AddAttribute(31, "onclick", EventCallback.Factory.Create(this, () => Choose(option)));
            builder.AddContent(32, isChosen ? Texts.Chosen : multiple ? Texts.Add : Texts.Choose);
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private string RangeText(PageWindow window) =>
        Format(Texts.ChosenRangeFormat, window.Skip + 1, Math.Min(window.Skip + window.Take, window.Total), window.Total);

    private void Announce(string text)
    {
        _announcements++;
        _announcement = _announcements % 2 == 0 ? text + AnnouncementToggle : text;
    }

    private async Task Toggle()
    {
        _open = !_open;
        var generation = ++_searchGeneration;
        if (!_open || _search is not null)
        {
            return;
        }

        _search = new OptionSearch<TOption>(Search, SearchLimit) { Debounce = Debounce, MinQueryLength = MinQueryLength };
        await _search.Start(_disposal.Token).ConfigureAwait(true);
        AnnounceSearch(_search, generation);
    }

    private async Task SearchFor(ChangeEventArgs args)
    {
        if (_search is not { } search)
        {
            return;
        }

        var generation = ++_searchGeneration;
        await search.Search(args.Value?.ToString(), _disposal.Token).ConfigureAwait(true);
        AnnounceSearch(search, generation);
    }

    private void AnnounceSearch(OptionSearch<TOption> search, int generation)
    {
        if (generation != _searchGeneration || !_open)
        {
            return;
        }

        if (search.IsQueryTooShort)
        {
            Announce(Format(Texts.QueryTooShortFormat, MinQueryLength));
            return;
        }

        var status = search.Query.Length == 0
            ? search.Status(Texts.FirstOptionsFormat, Texts.NoOptions)
            : search.Status(Texts.MatchesFormat, Texts.NoMatchesFormat);
        if (status is not null)
        {
            Announce(status.TrimEnd(AnnouncementToggle));
        }
    }

    private async Task Choose(TOption option)
    {
        var key = Catalog.KeyOf(option);
        var chosen = Chosen;
        if (chosen.Contains(key))
        {
            return;
        }

        if (!IsMultiple)
        {
            Catalog.Remember(option);
            _open = false;
            _focus = FocusTarget.Toggle;
            await ValueChanged.InvokeAsync(key).ConfigureAwait(true);
            return;
        }

        if (IsFull)
        {
            Announce(Format(Texts.FullFormat, MaxCount));
            return;
        }

        Catalog.Remember(option);
        await RaiseValues([.. chosen, key]).ConfigureAwait(true);
    }

    private async Task Remove(TKey key)
    {
        _removeButtons.Remove(key);
        _earlierButtons.Remove(key);
        _laterButtons.Remove(key);
        if (!IsMultiple)
        {
            _focus = FocusTarget.Toggle;
            await ValueChanged.InvokeAsync(null).ConfigureAwait(true);
            return;
        }

        while (true)
        {
            var chosen = Chosen;
            var index = IndexOf(chosen, key);
            if (index < 0)
            {
                return;
            }

            TKey[] remaining = [.. chosen.Where(item => !item.Equals(key))];
            var window = PageWindow.Create(_chosenPage, ChosenPageSize, remaining.Length);
            if (await LoadOptions(remaining.Skip(window.Skip).Take(window.Take)).ConfigureAwait(true))
            {
                continue;
            }

            _chosenPage = window.Page;
            if (remaining.Length == 0)
            {
                _focus = FocusTarget.Toggle;
            }
            else
            {
                _focus = FocusTarget.Remove;
                _focusKey = remaining[Math.Min(index, remaining.Length - 1)];
            }

            await RaiseValues(remaining).ConfigureAwait(true);
            return;
        }
    }

    private async Task Move(TKey key, int offset)
    {
        while (true)
        {
            var chosen = Chosen;
            var index = IndexOf(chosen, key);
            var target = index + offset;
            if (index < 0 || target < 0 || target >= chosen.Count)
            {
                return;
            }

            var moved = chosen.ToList();
            moved.RemoveAt(index);
            moved.Insert(target, key);
            var window = PageWindow.Create((target / ChosenPageSize) + 1, ChosenPageSize, moved.Count);
            if (await LoadOptions(moved.Skip(window.Skip).Take(window.Take)).ConfigureAwait(true))
            {
                continue;
            }

            _chosenPage = window.Page;
            _focus = offset < 0 ? FocusTarget.Earlier : FocusTarget.Later;
            _focusKey = key;
            Announce(Format(Texts.MovedFormat, LabelFor(Catalog.Find(key)), target + 1, moved.Count));
            await RaiseValues(moved).ConfigureAwait(true);
            return;
        }
    }

    private async Task ShowChosenPage(int? page)
    {
        if (page is not { } number)
        {
            return;
        }

        while (true)
        {
            var window = PageWindow.Create(number, ChosenPageSize, Chosen.Count);
            if (await LoadOptions(Chosen.Skip(window.Skip).Take(window.Take)).ConfigureAwait(true))
            {
                continue;
            }

            _chosenPage = window.Page;
            Announce(RangeText(window));
            return;
        }
    }

    private async Task RaiseValues(IReadOnlyList<TKey> values)
    {
        _raisedValues = values;
        await ValuesChanged.InvokeAsync(values).ConfigureAwait(true);
    }

    private async Task<bool> LoadOptions(IEnumerable<TKey> keys)
    {
        if (Lookup is null)
        {
            return false;
        }

        TKey[] missing = [.. keys.Where(key => Catalog.Find(key) is null && _requested.Add(key))];
        if (missing.Length == 0)
        {
            return false;
        }

        var options = await Lookup(missing, _disposal.Token).ConfigureAwait(true)
            ?? throw new InvalidOperationException("The lookup returned no options.");
        foreach (var option in options)
        {
            Catalog.Remember(option);
        }

        return true;
    }

    private static int IndexOf(IReadOnlyList<TKey> keys, TKey key)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (keys[index].Equals(key))
            {
                return index;
            }
        }

        return -1;
    }
}
