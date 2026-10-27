using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PandaBear.Blazor;

/// <summary>Moves focus without failing when the circuit or renderer has already gone away.</summary>
internal static class FocusHelper
{
    /// <summary>Focuses an element, ignoring the exceptions raised when the circuit disconnects or is disposed.</summary>
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
        catch (TaskCanceledException)
        {
            // The JavaScript call was canceled while the circuit shut down.
        }
        catch (ObjectDisposedException)
        {
            // The renderer or JavaScript runtime was disposed first.
        }
    }
}
