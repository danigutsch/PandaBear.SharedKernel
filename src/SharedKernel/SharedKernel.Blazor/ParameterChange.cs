namespace PandaBear.Blazor;

/// <summary>How a parameter value compares with the one a <see cref="ParameterTracker{T}"/> saw before.</summary>
public enum ParameterChange
{
    /// <summary>The first value; the component is loading for the first time.</summary>
    Initial,

    /// <summary>The same value as before; there is nothing to reload.</summary>
    Unchanged,

    /// <summary>A different value; the component instance is reused and its state must follow the parameter.</summary>
    Changed,
}
