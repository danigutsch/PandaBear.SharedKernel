using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor.Tests;

/// <summary>Renders the form field component for tests.</summary>
internal static class FormFieldRendering
{
    /// <summary>Renders a field labelled "Your name" with the control id "name-input" inside the edit context.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The edit context the field is rendered in.</param>
    /// <param name="model">The model whose name is bound.</param>
    /// <returns>The rendered component tree.</returns>
    public static IRenderedComponent<CascadingValue<EditContext>> RenderField(this BunitContext context, EditContext editContext, ServerValidationModel model)
        => context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, editContext)
            .AddChildContent<FormField<string>>(field => field
                .Add(item => item.Label, "Your name")
                .Add(item => item.InputId, "name-input")
                .Add(item => item.For, () => model.Name)
                .AddChildContent("<input id=\"name-input\" />")));
}
