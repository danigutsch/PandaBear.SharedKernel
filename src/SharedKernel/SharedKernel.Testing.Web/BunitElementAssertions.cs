namespace SharedKernel.Testing.Web;

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
