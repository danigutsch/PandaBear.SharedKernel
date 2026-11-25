using Microsoft.Playwright;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// Locates headings, links and buttons by their exact accessible name, the way a user and a screen reader find them. Playwright's
/// <c>GetByRole</c> matches a name as a case-insensitive substring by default, so <c>"Save"</c> also finds "Save draft"; these helpers set
/// <c>Exact = true</c>.
/// </summary>
public static class PlaywrightRoleLocatorExtensions
{
    /// <summary>Locates the headings with exactly this accessible name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The accessible name.</param>
    /// <param name="level">The heading level (1 to 6); any level when <see langword="null"/>.</param>
    /// <returns>The locator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not between 1 and 6.</exception>
    public static ILocator GetHeading(this IPage page, string name, int? level = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(name);
        if (level is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(level));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 6, nameof(level));
        }

        return page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true, Level = level });
    }

    /// <summary>Locates the headings of one level, whatever their name, such as the one <c>h1</c> of a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="level">The heading level (1 to 6).</param>
    /// <returns>The locator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not between 1 and 6.</exception>
    public static ILocator GetHeading(this IPage page, int level)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6);
        return page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = level });
    }

    /// <summary>Locates the links with exactly this accessible name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The locator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    public static ILocator GetLink(this IPage page, string name)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(name);
        return page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name, Exact = true });
    }

    /// <summary>Locates the buttons with exactly this accessible name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The locator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    public static ILocator GetButton(this IPage page, string name)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(name);
        return page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });
    }
}
