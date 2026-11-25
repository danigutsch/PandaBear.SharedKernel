using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor;

/// <summary>
/// A number typed as text: an <c>input</c> without <c>type="number"</c>, so the browser neither rounds nor clears what was typed and
/// adds no range or spinner. It keeps the typed text (<see cref="ParsedTextInputState{TValue}"/>), writes the number back through
/// <see cref="ValueChanged"/> only when the text is one, and when the form is validated files <see cref="Message"/> (or
/// <see cref="DecimalsMessage"/>) on the field while the text is not. Render it inside a <see cref="FormField{TValue}"/> or
/// <see cref="NamedFormField"/> and spread the field's attributes (<c>@attributes="context"</c>) for its <c>aria-describedby</c> and
/// <c>aria-invalid</c>.
/// </summary>
/// <remarks>
/// <para>
/// The value is an <see cref="int"/> or a <see cref="decimal"/>; a nullable one reads blank text as <see langword="null"/>, otherwise
/// blank text is refused. Surrounding white space is ignored and a leading sign is accepted. A decimal has at most one decimal
/// separator, which may be the culture's, a point or a comma, so <c>42,5</c> and <c>42.5</c> are both 42.5 in every culture; group
/// separators and exponents are refused, so <c>1.234,5</c> and <c>1e2</c> are not numbers. <see cref="MaxDecimals"/> then refuses
/// <c>1.500</c> for an amount with two decimals rather than reading it as 1.5 when it is most likely 1500.
/// </para>
/// <para>
/// The control asks for a number keyboard (<c>inputmode</c> <c>numeric</c> or <c>decimal</c>) unless <see cref="MayBeNegative"/> is
/// set, and turns off spell checking and autocomplete; attributes passed to the component, such as <c>id</c>, are added after them.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The bound type: <see cref="int"/>, <see cref="decimal"/> or a nullable one of them.</typeparam>
public sealed class NumberTextInput<TValue> : ComponentBase, IDisposable
{
    private static readonly Type Number = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    private static readonly bool Optional = Number != typeof(TValue);

    private readonly ParsedTextInputState<TValue> _state;
    private Expression<Func<TValue>>? _expression;
    private FieldIdentifier _expressionField;

    /// <summary>Creates the input.</summary>
    public NumberTextInput() => _state = new ParsedTextInputState<TValue>(Format, Parse);

    /// <summary>Gets or sets the value.</summary>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>Gets or sets the callback that receives the value whenever the typed text is a valid one.</summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Gets or sets the expression of the bound value, which <c>@bind-Value</c> sets; its field receives the message.</summary>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>
    /// Gets or sets the field that receives the message instead of the field of <see cref="ValueExpression"/>, such as
    /// <c>editContext.Field("prices[0]")</c> when validation names fields by a key.
    /// </summary>
    [Parameter]
    public FieldIdentifier? Field { get; set; }

    /// <summary>Gets or sets the message filed on the field while the text is not a number of the bound type, such as "Year must be a whole number, like 2027."</summary>
    [Parameter]
    [EditorRequired]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the most decimal places a decimal may be written with, counting trailing zeros, such as a currency's two for an
    /// amount. <see langword="null"/> (the default) sets no limit.
    /// </summary>
    [Parameter]
    public int? MaxDecimals { get; set; }

    /// <summary>Gets or sets the message for a decimal with more places than <see cref="MaxDecimals"/>; <see cref="Message"/> by default.</summary>
    [Parameter]
    public string? DecimalsMessage { get; set; }

    /// <summary>
    /// Gets or sets the culture whose decimal separator and negative sign write the value and are read back; the current culture by
    /// default.
    /// </summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value may be negative: the control then asks for the full keyboard, because a phone's
    /// number pad has no minus sign. A negative number is read either way.
    /// </summary>
    [Parameter]
    public bool MayBeNegative { get; set; }

    /// <summary>Gets or sets the attributes added to the <c>input</c>, such as its <c>id</c> and the field's accessibility attributes.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    private CultureInfo FormatCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <inheritdoc />
    public void Dispose() => _state.Dispose();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The bound type is not <see cref="int"/>, <see cref="decimal"/> or a nullable one of them.</exception>
    /// <exception cref="InvalidOperationException">
    /// The input is not inside an <c>EditForm</c>, has neither <see cref="Field"/> nor <see cref="ValueExpression"/>, or has no
    /// <see cref="Message"/>.
    /// </exception>
    protected override void OnParametersSet()
    {
        if (Number != typeof(int) && Number != typeof(decimal))
        {
            throw new NotSupportedException($"A number text input holds an int or a decimal, not {typeof(TValue).Name}.");
        }

        var context = CurrentEditContext
            ?? throw new InvalidOperationException("A number text input requires a cascading EditContext; render it inside an EditForm.");
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new InvalidOperationException("A number text input requires a message for text that is not a number.");
        }

        _state.Bind(context, Field ?? ExpressionField(), Value);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "inputmode", MayBeNegative ? null : Number == typeof(int) ? "numeric" : "decimal");
        builder.AddAttribute(2, "spellcheck", "false");
        builder.AddAttribute(3, "autocomplete", "off");
        builder.AddMultipleAttributes(4, AdditionalAttributes);
        builder.AddAttribute(5, "value", _state.Text);
        builder.AddAttribute(6, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, Change));
        builder.SetUpdatesAttributeName("value");
        builder.CloseElement();
    }

    private FieldIdentifier ExpressionField()
    {
        if (ValueExpression is null)
        {
            throw new InvalidOperationException("A number text input requires a Field or a ValueExpression (set by @bind-Value).");
        }

        if (!ReferenceEquals(ValueExpression, _expression))
        {
            _expression = ValueExpression;
            _expressionField = FieldIdentifier.Create(ValueExpression);
        }

        return _expressionField;
    }

    private Task Change(ChangeEventArgs change) =>
        _state.TrySetText(change.Value as string ?? string.Empty, out var value) ? ValueChanged.InvokeAsync(value) : Task.CompletedTask;

    private string? Format(TValue value) => value switch
    {
        int whole => whole.ToString(FormatCulture),
        decimal number => number.ToString(FormatCulture),
        _ => null,
    };

    private (TValue Value, string? Error) Parse(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return (default!, Optional ? null : Message);
        }

        var format = FormatCulture.NumberFormat;
        if (Number == typeof(int))
        {
            return int.TryParse(trimmed, NumberStyles.AllowLeadingSign, format, out var whole) ? ((TValue)(object)whole, null) : (default!, Message);
        }

        var normalized = trimmed.Replace(format.NumberDecimalSeparator, ".", StringComparison.Ordinal).Replace(',', '.');
        var pointFormat = (NumberFormatInfo)format.Clone();
        pointFormat.NumberDecimalSeparator = ".";
        if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, pointFormat, out var number))
        {
            return (default!, Message);
        }

        return number.Scale > MaxDecimals ? (default!, DecimalsMessage ?? Message) : ((TValue)(object)number, null);
    }
}
