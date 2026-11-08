using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor.Tests;

/// <summary>Renders a form with a numeric input for tests.</summary>
internal static class NumericFormRendering
{
    /// <summary>Renders an edit form over the edit context with an <see cref="InputNumber{TValue}"/> bound to the quantity.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The edit context of the form.</param>
    /// <param name="model">The model whose quantity is bound.</param>
    /// <returns>The rendered form.</returns>
    public static IRenderedComponent<EditForm> RenderQuantityForm(this BunitContext context, EditContext editContext, NumericFormModel model)
        => context.Render<EditForm>(parameters => parameters
            .Add(form => form.EditContext, editContext)
            .Add(form => form.ChildContent, _ => builder =>
            {
                builder.OpenComponent<InputNumber<int>>(0);
                builder.AddComponentParameter(1, nameof(InputNumber<int>.Value), model.Quantity);
                builder.AddComponentParameter(2, nameof(InputNumber<int>.ValueChanged), EventCallback.Factory.Create<int>(model, value => model.Quantity = value));
                builder.AddComponentParameter(3, nameof(InputNumber<int>.ValueExpression), (System.Linq.Expressions.Expression<Func<int>>)(() => model.Quantity));
                builder.CloseComponent();
            }));
}
