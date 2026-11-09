using Microsoft.Playwright;

namespace PandaBear.Testing.Playwright;

/// <summary>
/// Provides structural accessibility assertions for a page in its current state.
/// </summary>
/// <remarks>
/// The checks read the DOM once, right now; they do not wait or retry, so call them after the page is interactive (see
/// <see cref="PlaywrightPageExtensions.GotoInteractive"/>) and after the state under test is on screen. They find common structural
/// problems only and do not replace a full audit such as axe-core. An accessible name here is a non-blank <c>aria-label</c>, a non-blank
/// <c>aria-labelledby</c> attribute (the referenced element is not resolved), or a non-blank <c>title</c>, or, for a form control, an associated label and, for a link
/// or button, its text, a descendant <c>img</c> with a non-blank <c>alt</c> or a descendant with a non-blank <c>aria-label</c> (an <c>svg</c> <c>title</c> counts as text).
/// Image and reset inputs are skipped along with hidden, submit and button inputs. A failed assertion throws an <see cref="PlaywrightException"/> that describes the offending elements.
/// </remarks>
public static class PlaywrightAccessibilityExtensions
{
    private const string UnlabelledControlsScript = """
        () => {
            const visible = el => el.checkVisibility({ visibilityProperty: true }) && !el.closest('[aria-hidden="true"]');
            const named = el => (el.getAttribute('aria-label') || '').trim()
                || (el.getAttribute('aria-labelledby') || '').trim()
                || (el.getAttribute('title') || '').trim()
                || (el.labels && el.labels.length > 0);
            return [...document.querySelectorAll('input, select, textarea')]
                .filter(el => !['hidden', 'submit', 'button', 'image', 'reset'].includes(el.type))
                .filter(visible)
                .filter(el => !named(el))
                .map(el => el.tagName.toLowerCase() + '#' + (el.id || el.name || '?'));
        }
        """;

    private const string UnnamedLinksAndButtonsScript = """
        () => {
            const visible = el => el.checkVisibility({ visibilityProperty: true }) && !el.closest('[aria-hidden="true"]');
            const named = el => (el.getAttribute('aria-label') || '').trim()
                || (el.getAttribute('aria-labelledby') || '').trim()
                || (el.textContent || '').trim()
                || (el.getAttribute('title') || '').trim()
                || [...el.querySelectorAll('img[alt], [aria-label]')]
                    .some(inner => (inner.getAttribute('alt') || inner.getAttribute('aria-label') || '').trim());
            return [...document.querySelectorAll('a[href], button')]
                .filter(visible)
                .filter(el => !named(el))
                .map(el => el.outerHTML.slice(0, 80));
        }
        """;

    private const string LevelOneHeadingCountScript = "() => document.querySelectorAll('h1').length";

    private const string DocumentLanguageScript = "() => document.documentElement.lang";

    private const string ScrollsHorizontallyScript =
        "() => document.scrollingElement.scrollWidth > document.scrollingElement.clientWidth";

    private const string FocusWithinScript =
        "selector => !!document.activeElement && !!document.activeElement.closest(selector)";

    private const string ActiveElementScript =
        "() => document.activeElement ? document.activeElement.outerHTML.slice(0, 80) : 'nothing'";

    /// <summary>Verifies that every visible form control has an accessible name.</summary>
    /// <remarks>Invisible elements and hidden, submit, button, image and reset inputs are skipped. A control is named by an associated label, <c>aria-label</c>, <c>aria-labelledby</c> or <c>title</c>.</remarks>
    /// <param name="page">The page to inspect.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when a control has no accessible name; the message lists them as <c>tag#id</c>.</exception>
    public static Task ShouldHaveNoUnlabelledControls(this IPage page) => Verify(page, null, UnlabelledControls);

    /// <summary>Verifies that every visible form control has an accessible name.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when a control has no accessible name.</exception>
    public static Task ShouldHaveNoUnlabelledControls(this IPage page, string userMessage) => Verify(page, Required(userMessage), UnlabelledControls);

    /// <summary>Verifies that every link with an <c>href</c> and every button has an accessible name.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when a link or button has no name; the message quotes the start of each element's markup.</exception>
    public static Task ShouldHaveNoUnnamedLinksOrButtons(this IPage page) => Verify(page, null, UnnamedLinksOrButtons);

    /// <summary>Verifies that every link with an <c>href</c> and every button has an accessible name.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when a link or button has no name.</exception>
    public static Task ShouldHaveNoUnnamedLinksOrButtons(this IPage page, string userMessage) => Verify(page, Required(userMessage), UnnamedLinksOrButtons);

    /// <summary>Verifies that the page has exactly one first-level heading.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when the page has none or more than one <c>h1</c>.</exception>
    public static Task ShouldHaveSingleLevelOneHeading(this IPage page) => Verify(page, null, SingleLevelOneHeading);

    /// <summary>Verifies that the page has exactly one first-level heading.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when the page has none or more than one <c>h1</c>.</exception>
    public static Task ShouldHaveSingleLevelOneHeading(this IPage page, string userMessage) => Verify(page, Required(userMessage), SingleLevelOneHeading);

    /// <summary>Verifies the language of the document, the <c>lang</c> attribute of its root element.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="expectedLanguage">The expected language tag, for example <c>en</c>. Compared ignoring case.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="expectedLanguage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expectedLanguage"/> is empty or white space.</exception>
    /// <exception cref="PlaywrightException">Thrown when the language differs.</exception>
    public static Task ShouldHaveLanguage(this IPage page, string expectedLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedLanguage);

        return Verify(page, null, check => DocumentLanguage(check, expectedLanguage));
    }

    /// <summary>Verifies the language of the document, the <c>lang</c> attribute of its root element.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="expectedLanguage">The expected language tag, for example <c>en</c>. Compared ignoring case.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/>, <paramref name="expectedLanguage"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expectedLanguage"/> is empty or white space.</exception>
    /// <exception cref="PlaywrightException">Thrown when the language differs.</exception>
    public static Task ShouldHaveLanguage(this IPage page, string expectedLanguage, string userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedLanguage);

        return Verify(page, Required(userMessage), check => DocumentLanguage(check, expectedLanguage));
    }

    /// <summary>Verifies that the page does not scroll horizontally at its current viewport size.</summary>
    /// <remarks>Set the viewport first, for example <c>page.SetViewportSizeAsync(375, 800)</c>, to check a phone width.</remarks>
    /// <param name="page">The page to inspect.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when the document is wider than the viewport.</exception>
    public static Task ShouldNotScrollHorizontally(this IPage page) => Verify(page, null, NoHorizontalScroll);

    /// <summary>Verifies that the page does not scroll horizontally at its current viewport size.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when the document is wider than the viewport.</exception>
    public static Task ShouldNotScrollHorizontally(this IPage page, string userMessage) => Verify(page, Required(userMessage), NoHorizontalScroll);

    /// <summary>Verifies that keyboard focus is on an element inside an element that matches a CSS selector, or on that element itself.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="containerSelector">The CSS selector of the container, for example <c>[role='alert']</c>.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="containerSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerSelector"/> is empty or white space.</exception>
    /// <exception cref="PlaywrightException">Thrown when focus is elsewhere; the message quotes the focused element.</exception>
    public static Task ShouldHaveFocusWithin(this IPage page, string containerSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerSelector);

        return Verify(page, null, check => FocusWithin(check, containerSelector));
    }

    /// <summary>Verifies that keyboard focus is on an element inside an element that matches a CSS selector, or on that element itself.</summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="containerSelector">The CSS selector of the container, for example <c>[role='alert']</c>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/>, <paramref name="containerSelector"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerSelector"/> is empty or white space.</exception>
    /// <exception cref="PlaywrightException">Thrown when focus is elsewhere.</exception>
    public static Task ShouldHaveFocusWithin(this IPage page, string containerSelector, string userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerSelector);

        return Verify(page, Required(userMessage), check => FocusWithin(check, containerSelector));
    }

    /// <summary>
    /// Verifies the structure that most pages share: named form controls, named links and buttons, one first-level heading and no horizontal
    /// scroll. Every check runs and one failure reports all problems.
    /// </summary>
    /// <param name="page">The page to inspect.</param>
    /// <returns>A task that completes when every check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when any check fails.</exception>
    public static Task ShouldHaveAccessibleStructure(this IPage page) => Verify(page, null, AccessibleStructure);

    /// <summary>
    /// Verifies the structure that most pages share: named form controls, named links and buttons, one first-level heading and no horizontal
    /// scroll. Every check runs and one failure reports all problems.
    /// </summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>A task that completes when every check passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">Thrown when any check fails.</exception>
    public static Task ShouldHaveAccessibleStructure(this IPage page, string userMessage) => Verify(page, Required(userMessage), AccessibleStructure);

    private static string Required(string userMessage)
    {
        ArgumentNullException.ThrowIfNull(userMessage);
        return userMessage;
    }

    private static async Task Verify(IPage page, string? userMessage, Func<IPage, Task<string?>> check)
    {
        ArgumentNullException.ThrowIfNull(page);

        var problem = await check(page).ConfigureAwait(false);
        if (problem is not null)
        {
            throw new PlaywrightException(userMessage is null ? problem : userMessage + Environment.NewLine + problem);
        }
    }

    private static Task<string?> UnlabelledControls(IPage page) =>
        ListProblems(page, UnlabelledControlsScript, "Expected every form control to have an accessible name, but these have none: ");

    private static Task<string?> UnnamedLinksOrButtons(IPage page) =>
        ListProblems(page, UnnamedLinksAndButtonsScript, "Expected every link and button to have an accessible name, but these have none: ");

    private static async Task<string?> ListProblems(IPage page, string script, string heading)
    {
        var found = await page.EvaluateAsync<string[]>(script).ConfigureAwait(false);
        return found is { Length: > 0 } ? heading + string.Join("; ", found) : null;
    }

    private static async Task<string?> SingleLevelOneHeading(IPage page)
    {
        var count = await page.EvaluateAsync<int>(LevelOneHeadingCountScript).ConfigureAwait(false);
        return count == 1 ? null : $"Expected exactly one first-level heading (h1), but found {count}.";
    }

    private static async Task<string?> DocumentLanguage(IPage page, string expectedLanguage)
    {
        var language = await page.EvaluateAsync<string>(DocumentLanguageScript).ConfigureAwait(false);
        return string.Equals(language, expectedLanguage, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Expected the document language to be '{expectedLanguage}', but it is '{language}'.";
    }

    private static async Task<string?> NoHorizontalScroll(IPage page)
    {
        var scrolls = await page.EvaluateAsync<bool>(ScrollsHorizontallyScript).ConfigureAwait(false);
        return scrolls ? "Expected the page not to scroll horizontally, but its content is wider than the viewport." : null;
    }

    private static async Task<string?> FocusWithin(IPage page, string containerSelector)
    {
        var inside = await page.EvaluateAsync<bool>(FocusWithinScript, containerSelector).ConfigureAwait(false);
        if (inside)
        {
            return null;
        }

        var active = await page.EvaluateAsync<string>(ActiveElementScript).ConfigureAwait(false);
        return $"Expected keyboard focus inside '{containerSelector}', but it is on: {active}";
    }

    private static async Task<string?> AccessibleStructure(IPage page)
    {
        var problems = new List<string>();
        foreach (var check in new Func<IPage, Task<string?>>[] { UnlabelledControls, UnnamedLinksOrButtons, SingleLevelOneHeading, NoHorizontalScroll })
        {
            var problem = await check(page).ConfigureAwait(false);
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        return problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);
    }
}
