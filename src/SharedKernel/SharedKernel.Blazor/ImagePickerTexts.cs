namespace SharedKernel.Blazor;

/// <summary>
/// The copy of an <see cref="ImagePicker{TKey, TOption}"/>, in English by default. Replace any of it with <c>with</c> or an object
/// initializer to localize the picker or to name what it picks. A format is a composite format; its placeholders are listed with it.
/// </summary>
public sealed record ImagePickerTexts
{
    /// <summary>Gets the text shown while nothing is chosen.</summary>
    public string Empty { get; init; } = "No image chosen.";

    /// <summary>Gets the label of a chosen key whose option the application no longer has, such as an image deleted since.</summary>
    public string Missing { get; init; } = "An image that is no longer in the library";

    /// <summary>Gets the visible text of the button that opens the search.</summary>
    public string Find { get; init; } = "Find an image";

    /// <summary>Gets the visible text of the button that closes the search.</summary>
    public string Close { get; init; } = "Close the image search";

    /// <summary>Gets the accessible name of the find and close button: <c>{0}</c> its visible text, <c>{1}</c> the picker's label.</summary>
    public string ToggleFormat { get; init; } = "{0}, {1}";

    /// <summary>Gets the label of the search field.</summary>
    public string SearchLabel { get; init; } = "Search images";

    /// <summary>Gets the optional help under the search field, such as what the search matches; <see langword="null"/> for none.</summary>
    public string? SearchHelp { get; init; }

    /// <summary>Gets the announcement of the options offered for an empty search: <c>{0}</c> shown, <c>{1}</c> in all.</summary>
    public string FirstOptionsFormat { get; init; } = "Showing the newest {0} of {1} images.";

    /// <summary>Gets the announcement of an empty search that finds nothing, because there are no options at all.</summary>
    public string NoOptions { get; init; } = "There are no images in the library yet.";

    /// <summary>Gets the announcement of the matches of a search: <c>{0}</c> shown, <c>{1}</c> in all, <c>{2}</c> the query.</summary>
    public string MatchesFormat { get; init; } = "Showing {0} of {1} images that match ‘{2}’.";

    /// <summary>Gets the announcement of a search that matches nothing: <c>{0}</c> the query.</summary>
    public string NoMatchesFormat { get; init; } = "No images match ‘{0}’.";

    /// <summary>Gets the hint that a query is too short: <c>{0}</c> the shortest length.</summary>
    public string QueryTooShortFormat { get; init; } = "Type at least {0} characters.";

    /// <summary>Gets the hint under results that show only part of the matches: <c>{0}</c> the number shown.</summary>
    public string TruncatedFormat { get; init; } = "Only the first {0} are shown. Type more to find the others.";

    /// <summary>Gets the visible text of a result's button in single mode.</summary>
    public string Choose { get; init; } = "Choose";

    /// <summary>Gets the accessible name of a result's button in single mode: <c>{0}</c> the option's label.</summary>
    public string ChooseFormat { get; init; } = "Choose {0}";

    /// <summary>Gets the visible text of a result's button in multiple mode.</summary>
    public string Add { get; init; } = "Add";

    /// <summary>Gets the accessible name of a result's button in multiple mode: <c>{0}</c> the option's label.</summary>
    public string AddFormat { get; init; } = "Add {0}";

    /// <summary>Gets the visible text of the button of a result that is already chosen.</summary>
    public string Chosen { get; init; } = "Chosen";

    /// <summary>Gets the accessible name of the button of a result that is already chosen: <c>{0}</c> the option's label.</summary>
    public string ChosenFormat { get; init; } = "{0}, chosen";

    /// <summary>Gets the announcement when the most options are chosen and another is added: <c>{0}</c> the most.</summary>
    public string FullFormat { get; init; } = "You can choose at most {0} images. Remove one to add another.";

    /// <summary>Gets the visible text of a chosen option's remove button.</summary>
    public string Remove { get; init; } = "Remove";

    /// <summary>Gets the accessible name of a chosen option's remove button: <c>{0}</c> the option's label.</summary>
    public string RemoveFormat { get; init; } = "Remove {0}";

    /// <summary>Gets the visible text of a chosen option's move-earlier button.</summary>
    public string MoveEarlier { get; init; } = "Move earlier";

    /// <summary>Gets the accessible name of a chosen option's move-earlier button: <c>{0}</c> the option's label.</summary>
    public string MoveEarlierFormat { get; init; } = "Move {0} earlier";

    /// <summary>Gets the visible text of a chosen option's move-later button.</summary>
    public string MoveLater { get; init; } = "Move later";

    /// <summary>Gets the accessible name of a chosen option's move-later button: <c>{0}</c> the option's label.</summary>
    public string MoveLaterFormat { get; init; } = "Move {0} later";

    /// <summary>Gets the announcement after a move: <c>{0}</c> the option's label, <c>{1}</c> its new position, <c>{2}</c> the number chosen.</summary>
    public string MovedFormat { get; init; } = "{0} moved to position {1} of {2}.";

    /// <summary>Gets the range of the chosen options shown: <c>{0}</c> the first position, <c>{1}</c> the last, <c>{2}</c> the number chosen.</summary>
    public string ChosenRangeFormat { get; init; } = "Showing {0} to {1} of {2} chosen images.";

    /// <summary>Gets the visible text of the button that shows the previous page of chosen options.</summary>
    public string Previous { get; init; } = "Previous images";

    /// <summary>Gets the visible text of the button that shows the next page of chosen options.</summary>
    public string Next { get; init; } = "Next images";

    /// <summary>Gets the accessible name of the previous and next buttons: <c>{0}</c> the visible text, <c>{1}</c> the picker's label.</summary>
    public string PageFormat { get; init; } = "{0}, {1}";
}
