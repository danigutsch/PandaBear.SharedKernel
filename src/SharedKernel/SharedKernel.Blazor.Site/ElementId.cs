namespace PandaBear.Blazor.Site;

/// <summary>Checks an element id that a component writes into <c>id</c>, such as the id of a heading that a link or <c>aria-labelledby</c> points at.</summary>
public static class ElementId
{
    /// <summary>Refuses an id that is empty or holds white space; <see langword="null"/> is accepted (no id).</summary>
    /// <param name="id">The id.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or holds white space.</exception>
    public static void Require(string? id, string parameterName)
    {
        if (id is not null && (id.Length == 0 || id.AsSpan().ContainsAnyWhiteSpace()))
        {
            throw new ArgumentException("A heading id must not be empty or contain white space.", parameterName);
        }
    }
}
