using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PandaBear.Blazor;

/// <summary>Moves focus without failing when the circuit or renderer has already gone away.</summary>
internal static class FocusHelper
{
    /// <summary>
    /// Focuses an element, ignoring the exceptions raised when the circuit disconnects or is disposed.
    /// A canceled call needs no catch: the renderer already treats a canceled lifecycle task as not faulted.
    /// </summary>
    public static async ValueTask TryFocus(ElementReference element)
    {
        try
        {
            await element.FocusAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; there is nothing left to focus.
        }
        catch (ObjectDisposedException)
        {
            // The renderer or JavaScript runtime was disposed first.
        }
    }
}
