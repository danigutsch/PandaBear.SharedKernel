namespace PandaBear.CI.Tool;

/// <summary>The validation plan that <c>select</c> writes to <c>selection.json</c>.</summary>
/// <param name="Revision">The selected <c>HEAD</c>, or <c>unavailable</c> outside a Git checkout.</param>
/// <param name="ComparisonBase">The <c>--base</c> value, if any.</param>
/// <param name="FullValidation">Whether every test project is selected.</param>
/// <param name="BuildRequired">Whether any project is selected.</param>
/// <param name="DocumentationOnly">Whether the change selects no project because it touches only documentation, or nothing.</param>
/// <param name="Reasons">Why each changed path selected what it did, or why every test project is selected.</param>
/// <param name="BuildProjects">The selected non-test projects; for full validation, every solution project.</param>
/// <param name="Slices">Every configured slice, in configuration order.</param>
internal sealed record SelectionPlan(
    string Revision,
    string? ComparisonBase,
    bool FullValidation,
    bool BuildRequired,
    bool DocumentationOnly,
    string[] Reasons,
    string[] BuildProjects,
    SliceSelection[] Slices);
