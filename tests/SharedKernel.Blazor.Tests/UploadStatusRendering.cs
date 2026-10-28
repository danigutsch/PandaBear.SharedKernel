using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>Renders the upload status component for tests.</summary>
internal static class UploadStatusRendering
{
    /// <summary>The accessible name given to the status region.</summary>
    public const string RegionLabel = "Import upload status";

    /// <summary>Renders the component for a state.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="state">The state to show.</param>
    /// <param name="onCancel">The cancel callback.</param>
    /// <returns>The rendered component.</returns>
    public static IRenderedComponent<UploadStatus> RenderStatus(this BunitContext context, UploadOperationState state, Action? onCancel = null)
        => context.Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.OnCancel, EventCallback.Factory.Create(context, onCancel ?? (() => { }))));

    /// <summary>Renders the component for a state with caller-supplied detail and extra attributes.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="state">The state to show.</param>
    /// <param name="detail">The detail fragment, or <see langword="null" /> for none.</param>
    /// <returns>The rendered component.</returns>
    public static IRenderedComponent<UploadStatus> RenderStatusWithDetail(this BunitContext context, UploadOperationState state, RenderFragment<UploadOperationState>? detail)
        => context.Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.Detail, detail)
            .AddUnmatched("class", "upload"));

    /// <summary>Creates a detail fragment that renders text computed from the state.</summary>
    /// <param name="text">Computes the text from the state.</param>
    /// <returns>The detail fragment.</returns>
    public static RenderFragment<UploadOperationState> TextDetail(Func<UploadOperationState, string> text)
        => state => builder => builder.AddContent(0, text(state));
}
