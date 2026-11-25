using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace SharedKernel.Blazor.Tests;

/// <summary>Renders the number text input inside a form field for tests.</summary>
internal static class NumberTextInputRendering
{
    /// <summary>The id of the rendered control.</summary>
    public const string InputId = "amount";

    /// <summary>The message the rendered input reports for text that is not a number.</summary>
    public const string Message = "Amount must be a number, like 42.5.";

    /// <summary>
    /// Renders a <see cref="FormField{TValue}"/> labelled "Amount" over the field with a <see cref="NumberTextInput{TValue}"/> bound to it,
    /// as <c>@bind-Value</c> does: the value is read from the model on every render and written back on change.
    /// </summary>
    /// <typeparam name="TValue">The type of the bound value.</typeparam>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The edit context the field is rendered in.</param>
    /// <param name="field">The bound model property.</param>
    /// <param name="write">Writes a changed value to the model.</param>
    /// <param name="configure">Sets further parameters of the input.</param>
    /// <returns>The rendered component tree.</returns>
    public static IRenderedComponent<CascadingValue<EditContext>> RenderNumberField<TValue>(
        this BunitContext context, EditContext editContext, Expression<Func<TValue>> field, Action<TValue> write,
        Action<ComponentParameterCollectionBuilder<NumberTextInput<TValue>>>? configure = null)
    {
        var read = field.Compile();
        return context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, editContext)
            .AddChildContent<FormField<TValue>>(formField => formField
                .Add(item => item.Label, "Amount")
                .Add(item => item.InputId, InputId)
                .Add(item => item.For, field)
                .Add<NumberTextInput<TValue>, IReadOnlyDictionary<string, object>>(item => item.ChildContent, attributes => input =>
                {
                    input
                        .Add(item => item.Value, read())
                        .Add(item => item.ValueChanged, write)
                        .Add(item => item.ValueExpression, field)
                        .Add(item => item.Message, Message)
                        .Add(item => item.AdditionalAttributes, new Dictionary<string, object>(attributes) { ["id"] = InputId });
                    configure?.Invoke(input);
                })));
    }

    /// <summary>
    /// Renders a <see cref="NamedFormField"/> over a field named by a server key with a <see cref="NumberTextInput{TValue}"/> that
    /// files its message under that key and has no value expression.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The edit context the field is rendered in.</param>
    /// <param name="field">The field the messages are filed under.</param>
    /// <returns>The rendered component tree.</returns>
    public static IRenderedComponent<CascadingValue<EditContext>> RenderNamedNumberField(this BunitContext context, EditContext editContext, FieldIdentifier field)
        => context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, editContext)
            .AddChildContent<NamedFormField>(namedField => namedField
                .Add(item => item.Field, field)
                .Add(item => item.Label, "Amount")
                .Add(item => item.InputId, InputId)
                .Add<NumberTextInput<decimal>, IReadOnlyDictionary<string, object>>(item => item.ChildContent, attributes => input => input
                    .Add(item => item.Value, 0m)
                    .Add(item => item.Field, field)
                    .Add(item => item.Message, Message)
                    .Add(item => item.AdditionalAttributes, new Dictionary<string, object>(attributes) { ["id"] = InputId }))));

    /// <summary>Sets the invariant culture, so the value is written with a decimal point.</summary>
    /// <typeparam name="TValue">The type of the bound value.</typeparam>
    /// <param name="input">The parameters of the input.</param>
    public static void Invariant<TValue>(ComponentParameterCollectionBuilder<NumberTextInput<TValue>> input) =>
        input.Add(item => item.Culture, CultureInfo.InvariantCulture);

    /// <summary>Requests validation of the form on the renderer's dispatcher, as a submit does.</summary>
    /// <param name="cut">The rendered tree.</param>
    /// <param name="editContext">The edit context of the form.</param>
    /// <returns>Whether the form is valid.</returns>
    public static async Task<bool> Submit(this IRenderedComponent<CascadingValue<EditContext>> cut, EditContext editContext)
    {
        var valid = false;
        await cut.InvokeAsync(async () => valid = await editContext.ValidateAsync(Xunit.TestContext.Current.CancellationToken));
        return valid;
    }

    /// <summary>Types text into the input and leaves it, which raises its <c>change</c> event.</summary>
    /// <param name="cut">The rendered tree.</param>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when the event is handled.</returns>
    public static Task Type(this IRenderedComponent<CascadingValue<EditContext>> cut, string text) =>
        cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = text });
}
