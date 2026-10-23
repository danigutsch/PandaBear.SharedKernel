using Terminal.Gui.Input;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// Every key of the review UI. The window binds these keys and the help lists them, so the help shows exactly what the
/// window does. Tab, Shift+Tab, and the arrow keys move between and within the lists as Terminal.Gui defines.
/// </summary>
internal static class ReviewKeys
{
    /// <summary>Lists every key.</summary>
    public static ReviewKey Help { get; } = new(Key.F1, ReviewArea.Everywhere, "List every key");

    /// <summary>Moves to the groups list.</summary>
    public static ReviewKey Groups { get; } = new(Key.F2, ReviewArea.Everywhere, "Go to the groups list");

    /// <summary>Shows the field table.</summary>
    public static ReviewKey Fields { get; } = new(Key.F3, ReviewArea.Everywhere, "Go to the fields of the selected group");

    /// <summary>Shows the findings table.</summary>
    public static ReviewKey Findings { get; } = new(Key.F4, ReviewArea.Everywhere, "Go to the findings of the selected group");

    /// <summary>Lists the groups of the next status filter.</summary>
    public static ReviewKey NextFilter { get; } = new(Key.F7, ReviewArea.Everywhere, "Next group filter: all, pending, conflict, suggested, identical, unique, has findings");

    /// <summary>Moves to the search field.</summary>
    public static ReviewKey Search { get; } = new(Key.F.WithCtrl, ReviewArea.Everywhere, "Search the groups by item name");

    /// <summary>Saves the decisions.</summary>
    public static ReviewKey Save { get; } = new(Key.S.WithCtrl, ReviewArea.Everywhere, "Save the decisions file");

    /// <summary>Writes the merged vault.</summary>
    public static ReviewKey Write { get; } = new(Key.W.WithCtrl, ReviewArea.Everywhere, "Write the merged vault once nothing needs a decision");

    /// <summary>Shares the notes of every item whose notes hold no finding.</summary>
    public static ReviewKey ShareNotes { get; } = new(Key.F8, ReviewArea.Everywhere, "Show the notes of every item whose notes hold no finding in catalogs; asks first");

    /// <summary>Quits.</summary>
    public static ReviewKey Quit { get; } = new(Key.Q.WithCtrl, ReviewArea.Everywhere, "Quit; asks first when decisions are not saved");

    /// <summary>Quits, as Terminal.Gui's default quit key.</summary>
    public static ReviewKey Escape { get; } = new(Key.Esc, ReviewArea.Everywhere, "Quit, like Ctrl+Q");

    /// <summary>Opens the fields of the selected group.</summary>
    public static ReviewKey OpenGroup { get; } = new(Key.Enter, ReviewArea.Groups, "Go to the fields of the selected group");

    /// <summary>Marks the selected group for a link.</summary>
    public static ReviewKey Mark { get; } = new(Key.M, ReviewArea.Groups, "Mark the selected group for a link");

    /// <summary>Links the marked group with the selected group.</summary>
    public static ReviewKey Link { get; } = new(Key.L, ReviewArea.Groups, "Link the marked group with the selected group");

    /// <summary>Takes the selected member's value for the selected field.</summary>
    public static ReviewKey ChooseMember { get; } = new(Key.Enter, ReviewArea.Fields, "Take the value of the selected member's column");

    /// <summary>Takes the selected member's value for every conflicting field.</summary>
    public static ReviewKey ChooseForConflicts { get; } = new(Key.A, ReviewArea.Fields, "Take the selected member's value for every conflicting field");

    /// <summary>Drops the selected field from every member.</summary>
    public static ReviewKey ChooseEmpty { get; } = new(Key.E, ReviewArea.Fields, "Drop the field from every member");

    /// <summary>Removes the decision on the selected field.</summary>
    public static ReviewKey ClearChoice { get; } = new(Key.Delete, ReviewArea.Fields, "Remove the decision on the field");

    /// <summary>Reveals or masks the values of the selected field.</summary>
    public static ReviewKey Reveal { get; } = new(Key.V, ReviewArea.Fields, "Reveal or mask the values of the field");

    /// <summary>Splits the selected member out of the group.</summary>
    public static ReviewKey Split { get; } = new(Key.S, ReviewArea.Fields, "Split the selected member out of the group");

    /// <summary>Shows the selected member's notes in catalogs.</summary>
    public static ReviewKey ShareMemberNotes { get; } = new(Key.N, ReviewArea.Fields, "Show the selected item's notes in catalogs");

    /// <summary>Keeps the selected member's notes out of catalogs.</summary>
    public static ReviewKey PrivateMemberNotes { get; } = new(Key.P, ReviewArea.Fields, "Keep the selected item's notes out of catalogs");

    /// <summary>Moves the selected member's notes into a hidden field when a catalog is applied.</summary>
    public static ReviewKey HideMemberNotes { get; } = new(Key.H, ReviewArea.Fields, "Move the selected item's notes into a hidden field when a catalog is applied");

    /// <summary>Accepts the selected finding.</summary>
    public static ReviewKey Accept { get; } = new(Key.A, ReviewArea.Findings, "Accept the finding");

    /// <summary>Rejects the selected finding.</summary>
    public static ReviewKey Reject { get; } = new(Key.R, ReviewArea.Findings, "Reject the finding");

    /// <summary>Removes the decision on the selected finding.</summary>
    public static ReviewKey ClearFinding { get; } = new(Key.Delete, ReviewArea.Findings, "Remove the decision on the finding");

    /// <summary>Reveals or masks the value of the selected finding.</summary>
    public static ReviewKey RevealFinding { get; } = new(Key.V, ReviewArea.Findings, "Reveal or mask the value of the finding");

    /// <summary>Every key, in the order the help lists them.</summary>
    public static IReadOnlyList<ReviewKey> All
    {
        get;
    } =
    [
        Help, Groups, Fields, Findings, NextFilter, Search, Save, Write, ShareNotes, Quit, Escape,
        OpenGroup, Mark, Link,
        ChooseMember, ChooseForConflicts, ChooseEmpty, ClearChoice, Reveal, Split, ShareMemberNotes, PrivateMemberNotes, HideMemberNotes,
        Accept, Reject, ClearFinding, RevealFinding,
    ];
}
