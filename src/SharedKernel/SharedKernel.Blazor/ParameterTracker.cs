namespace PandaBear.Blazor;

/// <summary>
/// Tells a component whether a parameter changed between calls of <c>OnParametersSetAsync</c>. Blazor keeps one component
/// instance when the route changes between URLs that map to the same component (for example <c>/items/new</c> and <c>/items/{Id}/edit</c>),
/// and calls <c>OnParametersSet</c> again even when no value changed, so state loaded from a parameter must be reloaded only on a real change.
/// </summary>
/// <typeparam name="T">The parameter type.</typeparam>
public sealed class ParameterTracker<T>
{
    private bool _seen;
    private T? _current;

    /// <summary>Records the latest value and reports how it compares with the previous one.</summary>
    /// <param name="value">The current parameter value.</param>
    /// <returns><see cref="ParameterChange.Initial"/> for the first value, otherwise whether it differs from the previous one.</returns>
    public ParameterChange Update(T value)
    {
        if (!_seen)
        {
            _seen = true;
            _current = value;
            return ParameterChange.Initial;
        }

        if (EqualityComparer<T>.Default.Equals(_current, value))
        {
            return ParameterChange.Unchanged;
        }

        _current = value;
        return ParameterChange.Changed;
    }
}
