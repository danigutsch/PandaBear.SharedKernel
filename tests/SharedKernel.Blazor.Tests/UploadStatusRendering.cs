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
}
