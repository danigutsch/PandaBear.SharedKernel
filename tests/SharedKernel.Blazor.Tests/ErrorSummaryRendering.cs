namespace PandaBear.Blazor.Tests;

/// <summary>Renders the error summary component for tests.</summary>
internal static class ErrorSummaryRendering
{
    /// <summary>Renders the component with explicit parameters, including invalid ones.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="title">The title.</param>
    /// <param name="items">The items.</param>
    public static void RenderSummary(this BunitContext context, string title, IReadOnlyList<ErrorSummaryItem> items)
        => context.Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, title)
            .Add(component => component.Items, items));
}
