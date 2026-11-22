using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Testing.AspNetCore.TestApp;

/// <summary>An Interactive Server component that persists a text of a given length across prerendering.</summary>
public sealed class StateHolder : ComponentBase
{
    /// <summary>Gets or sets the number of characters to persist.</summary>
    [Parameter]
    public int Size { get; set; }

    /// <summary>Gets or sets the persisted text.</summary>
    [PersistentState]
    public string? Payload { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Payload ??= new string('x', Size);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.OpenElement(0, "p");
        builder.AddContent(1, Payload?.Length ?? 0);
        builder.CloseElement();
    }
}
