namespace PandaBear.Blazor.Site.Theming;

/// <summary>The result of checking a theme against the rules: errors that block <see cref="SiteTheme.Create" />, warnings, and every contrast check.</summary>
public sealed class SiteThemeReview
{
    /// <summary>Creates a review.</summary>
    /// <param name="errors">The rule violations.</param>
    /// <param name="warnings">The warnings.</param>
    /// <param name="contrast">The contrast checks.</param>
    internal SiteThemeReview(IReadOnlyList<ThemeIssue> errors, IReadOnlyList<ThemeIssue> warnings, IReadOnlyList<ContrastCheck> contrast)
    {
        Errors = errors;
        Warnings = warnings;
        Contrast = contrast;
    }

    /// <summary>Gets the rule violations.</summary>
    public IReadOnlyList<ThemeIssue> Errors { get; }

    /// <summary>Gets the findings that do not block saving, such as a heading larger than the level above.</summary>
    public IReadOnlyList<ThemeIssue> Warnings { get; }

    /// <summary>Gets the contrast checks, light scheme first, in a stable order.</summary>
    public IReadOnlyList<ContrastCheck> Contrast { get; }

    /// <summary>Gets a value indicating whether any rule is violated.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Returns the errors as messages by field path, the shape of <c>Result</c> validation errors.</summary>
    /// <returns>The messages by field.</returns>
    public IReadOnlyDictionary<string, string[]> ToValidationErrors()
    {
        var errors = new ThemeErrors();
        foreach (var issue in Errors)
        {
            errors.Add(issue.Field, issue.Message);
        }

        return errors.ToDictionary();
    }
}
