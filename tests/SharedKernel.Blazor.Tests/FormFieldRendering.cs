using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace SharedKernel.Blazor.Tests;

/// <summary>Renders the form field component for tests.</summary>
internal static class FormFieldRendering
{
    /// <summary>
    /// Renders a field labelled "Your name" with the control id "name-input" inside the edit context. The control is an
    /// <see cref="InputText"/> bound to the name that spreads the attributes the field hands it.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The edit context the field is rendered in.</param>
    /// <param name="model">The model whose name is bound.</param>
    /// <param name="describedBy">The ids passed to <see cref="FormField{TValue}.DescribedBy"/>, or <see langword="null"/>.</param>
    /// <returns>The rendered component tree.</returns>
    public static IRenderedComponent<CascadingValue<EditContext>> RenderField(this BunitContext context, EditContext editContext, ServerValidationModel model, string? describedBy = null)
        => context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, editContext)
            .AddChildContent<FormField<string>>(field => field
                .Add(item => item.Label, "Your name")
                .Add(item => item.InputId, "name-input")
                .Add(item => item.For, () => model.Name)
                .Add(item => item.DescribedBy, describedBy)
                .Add(item => item.ChildContent, NameInput(model))));

    /// <summary>Adds messages to the name field, replacing earlier ones, and tells the edit context that the validation state changed.</summary>
    /// <param name="editContext">The edit context.</param>
    /// <param name="store">The store that holds the messages.</param>
    /// <param name="messages">The messages; none clears the field.</param>
    public static void ShowNameMessages(this EditContext editContext, ValidationMessageStore store, params string[] messages)
    {
        store.Clear();
        store.Add(editContext.Field(nameof(ServerValidationModel.Name)), messages);
        editContext.NotifyValidationStateChanged();
    }

    private static RenderFragment<IReadOnlyDictionary<string, object>> NameInput(ServerValidationModel model) => attributes => builder =>
    {
        builder.OpenComponent<InputText>(0);
        builder.AddComponentParameter(1, nameof(InputText.Value), model.Name);
        builder.AddComponentParameter(2, nameof(InputText.ValueExpression), (Expression<Func<string>>)(() => model.Name));
        builder.AddComponentParameter(3, nameof(InputText.ValueChanged), EventCallback.Factory.Create<string>(model, value => model.Name = value));
        builder.AddAttribute(4, "id", "name-input");
        builder.AddMultipleAttributes(5, attributes);
        builder.CloseComponent();
    };
}
