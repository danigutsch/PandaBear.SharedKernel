namespace PandaBear.Testing.Web;

/// <summary>
/// Shared assertions for rendered web element metadata.
/// </summary>
public static class BunitElementAssertions
{
    /// <summary>
    /// Verifies that a class list contains the expected CSS class.
    /// </summary>
    /// <param name="classList">The rendered element class list.</param>
    /// <param name="className">The expected CSS class.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="className"/> is empty or consists only of white-space characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="classList"/> or <paramref name="className"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="classList"/> does not contain <paramref name="className"/>.</exception>
    public static void HasClass(IEnumerable<string> classList, string className)
    {
        ArgumentNullException.ThrowIfNull(classList);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        if (!classList.Contains(className, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Expected class list to contain '{className}'.");
        }
    }
}
