using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>Builds element references bound to the test context's JavaScript runtime, as a render's <c>@ref</c> capture would.</summary>
internal static class FocusTargets
{
    /// <summary>Creates an element reference with the given id.</summary>
    /// <param name="context">The bUnit context whose JavaScript runtime records the focus calls.</param>
    /// <param name="id">The reference id.</param>
    /// <returns>The reference.</returns>
    public static ElementReference Element(this BunitContext context, string id) =>
        new(id, new WebElementReferenceContext(context.JSInterop.JSRuntime));

    /// <summary>Gets the ids of the elements focused so far, in order.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <returns>The ids.</returns>
    public static string[] FocusedIds(this BunitContext context) =>
        [.. context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus")
            .Select(invocation => ((ElementReference)invocation.Arguments[0]!).Id)];
}
