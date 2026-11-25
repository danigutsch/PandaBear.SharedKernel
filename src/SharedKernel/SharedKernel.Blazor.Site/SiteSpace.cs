namespace SharedKernel.Blazor.Site;

/// <summary>A step of the spacing scale, the <c>--site-space-1</c> to <c>--site-space-6</c> tokens, or no space.</summary>
public enum SiteSpace
{
    /// <summary>No space.</summary>
    None = 0,

    /// <summary><c>--site-space-1</c>, 0.25rem by default.</summary>
    Space1 = 1,

    /// <summary><c>--site-space-2</c>, 0.5rem by default.</summary>
    Space2 = 2,

    /// <summary><c>--site-space-3</c>, 1rem by default.</summary>
    Space3 = 3,

    /// <summary><c>--site-space-4</c>, 1.5rem by default.</summary>
    Space4 = 4,

    /// <summary><c>--site-space-5</c>, 2.5rem by default.</summary>
    Space5 = 5,

    /// <summary><c>--site-space-6</c>, 4rem by default.</summary>
    Space6 = 6,
}
