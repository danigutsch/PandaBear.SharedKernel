using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SharedKernel.Blazor;

/// <summary>
/// Moves keyboard focus after a render without failing the page when there is nothing left to focus. Call it from
/// <c>OnAfterRenderAsync</c>, after the render that captured the element with <c>@ref</c>.
/// </summary>
public static class FocusHelper
{
    /// <summary>
    /// Focuses an element with <see cref="ElementReferenceExtensions.FocusAsync(ElementReference)"/>, and does nothing when there is
    /// nothing to focus: a reference that no render captured (<see langword="default"/>), a circuit that disconnected, a renderer or
    /// JavaScript runtime that was disposed first, or an element that a later render removed from the page (the browser reports it as
    /// a <see cref="JSException"/>). Focus is a convenience for the user; none of these leaves anything to move it to, so none of them
    /// may fail the page. A canceled call needs no catch: the renderer already treats a canceled lifecycle task as not faulted.
    /// </summary>
    /// <param name="element">The element to focus.</param>
    /// <returns>A task that completes when focus has moved or there was nothing to focus.</returns>
    public static async ValueTask TryFocus(ElementReference element)
    {
        if (element.Context is null)
        {
            return;
        }

        try
        {
            await element.FocusAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
