using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using static SharedKernel.Blazor.Tests.NumberTextInputRendering;
using TestContext = Xunit.TestContext;

namespace SharedKernel.Blazor.Tests;

/// <summary>
/// Tests the number text input: it keeps what was typed, writes a number back only when the text is one, and on validation reports
/// text that is not a number of its type on the field.
/// </summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class NumberTextInputTests : BunitContext
{
    private readonly NumberFormModel _model = new();

    [Fact]
    public async Task A_whole_number_is_a_text_input_with_a_number_keyboard_and_writes_the_typed_number_back()
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Quantity, value => _model.Quantity = value, Invariant);

        // Act
        await cut.Type("17");

        // Assert
        var input = cut.Find($"#{InputId}");
        (input.GetAttribute("type"), input.GetAttribute("inputmode"), input.GetAttribute("spellcheck"), input.GetAttribute("autocomplete"))
            .ShouldBe((null, "numeric", "false", "off"));
        (input.GetAttribute("value"), _model.Quantity).ShouldBe(("17", 17));
    }

    [Fact]
    public async Task Text_that_is_not_a_whole_number_stays_as_typed_and_is_reported_on_the_field_on_validation()
    {
        // Arrange
        _model.Quantity = 3;
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Quantity, value => _model.Quantity = value, Invariant);
        await cut.Type("1.5");
        var beforeValidation = cut.FindAll(".validation-message").Count;

        // Act
        var valid = await cut.Submit(context);

        // Assert
        (beforeValidation, valid, _model.Quantity).ShouldBe((0, false, 3));
        var input = cut.Find($"#{InputId}");
        (input.GetAttribute("value"), input.GetAttribute("aria-invalid"), input.GetAttribute("aria-describedby")).ShouldBe(("1.5", "true", $"{InputId}-error"));
        cut.Find($"#{InputId}-error").TextContent.ShouldBe(Message);
    }

    [Fact]
    public async Task Corrected_text_clears_the_message_on_the_next_validation()
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Quantity, value => _model.Quantity = value, Invariant);
        await cut.Type("first");
        _ = await cut.Submit(context);

        // Act
        await cut.Type("2");
        var valid = await cut.Submit(context);

        // Assert
        (valid, _model.Quantity).ShouldBe((true, 2));
        cut.FindAll(".validation-message").ShouldBeEmpty();
        cut.Find("input").HasAttribute("aria-invalid").ShouldBeFalse();
    }

    [Theory]
    [InlineData("42,5")]
    [InlineData("42.5")]
    [InlineData(" 42.5 ")]
    public async Task A_decimal_comma_or_point_is_read_as_the_decimal_separator_and_the_text_is_kept(string typed)
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, Invariant);

        // Act
        await cut.Type(typed);

        // Assert
        (_model.Price, cut.Find("input").GetAttribute("value"), cut.Find("input").GetAttribute("inputmode")).ShouldBe((42.5m, typed, "decimal"));
    }

    [Theory]
    [InlineData("1.234,5")]
    [InlineData("1,234.5")]
    [InlineData("1 234")]
    [InlineData("4,2,5")]
    [InlineData("1e2")]
    [InlineData("far")]
    [InlineData("")]
    public async Task A_decimal_with_group_separators_or_that_is_not_a_number_is_refused(string typed)
    {
        // Arrange
        _model.Price = 7m;
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, Invariant);
        await cut.Type(typed);

        // Act
        var valid = await cut.Submit(context);

        // Assert
        (valid, _model.Price, cut.Find("input").GetAttribute("value")).ShouldBe((false, 7m, typed));
        cut.Find($"#{InputId}-error").TextContent.ShouldBe(Message);
    }

    [Theory]
    [InlineData("1.500")]
    [InlineData("1,500")]
    [InlineData("1500.505")]
    public async Task A_decimal_with_more_places_than_allowed_is_refused_with_the_decimals_message_even_when_they_are_zeros(string typed)
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, input => input
            .Add(item => item.Culture, CultureInfo.InvariantCulture)
            .Add(item => item.MaxDecimals, 2)
            .Add(item => item.DecimalsMessage, "Amount must have at most 2 decimal places, like 1500.50."));
        await cut.Type(typed);

        // Act
        var valid = await cut.Submit(context);

        // Assert
        (valid, _model.Price).ShouldBe((false, 0m));
        cut.Find($"#{InputId}-error").TextContent.ShouldBe("Amount must have at most 2 decimal places, like 1500.50.");
    }

    [Fact]
    public async Task Without_a_decimals_message_too_many_places_report_the_message_and_allowed_places_are_accepted()
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, input => input
            .Add(item => item.Culture, CultureInfo.InvariantCulture)
            .Add(item => item.MaxDecimals, 0));
        await cut.Type("1500.5");
        _ = await cut.Submit(context);
        var refused = cut.Find($"#{InputId}-error").TextContent;

        // Act
        await cut.Type("1500");
        var valid = await cut.Submit(context);

        // Assert
        (refused, valid, _model.Price).ShouldBe((Message, true, 1500m));
    }

    [Fact]
    public async Task An_optional_number_cleared_to_blank_writes_null_and_is_valid()
    {
        // Arrange
        _model.Rank = 3;
        _model.Distance = 4.5m;
        var context = new EditContext(_model);
        var rank = this.RenderNumberField(context, () => _model.Rank, value => _model.Rank = value, Invariant);
        var distance = this.RenderNumberField(context, () => _model.Distance, value => _model.Distance = value, Invariant);

        // Act
        await rank.Type(" ");
        await distance.Type(string.Empty);
        var valid = await rank.Submit(context);

        // Assert
        (valid, _model.Rank, _model.Distance).ShouldBe((true, null, null));
    }

    [Fact]
    public async Task A_required_whole_number_cleared_to_blank_is_reported()
    {
        // Arrange
        _model.Quantity = 4;
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Quantity, value => _model.Quantity = value, Invariant);
        await cut.Type(string.Empty);

        // Act
        var valid = await cut.Submit(context);

        // Assert
        (valid, _model.Quantity, cut.Find($"#{InputId}-error").TextContent).ShouldBe((false, 4, Message));
    }

    [Fact]
    public async Task The_culture_writes_the_value_with_its_decimal_separator_without_grouping_and_reads_it_back()
    {
        // Arrange
        _model.Price = 1500.25m;
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, input => input
            .Add(item => item.Culture, CultureInfo.GetCultureInfo("pt-BR")));
        var shown = cut.Find("input").GetAttribute("value");

        // Act
        await cut.Type("-3,75");

        // Assert
        (shown, _model.Price).ShouldBe(("1500,25", -3.75m));
    }

    [Fact]
    public void Without_a_culture_the_current_culture_formats_the_value()
    {
        // Arrange
        using var culture = new CurrentCultureScope("de-DE");
        _model.Distance = 2.125m;
        var context = new EditContext(_model);

        // Act
        var cut = this.RenderNumberField(context, () => _model.Distance, value => _model.Distance = value);

        // Assert
        cut.Find("input").GetAttribute("value").ShouldBe("2,125");
    }

    [Fact]
    public void A_number_that_may_be_negative_asks_for_the_full_keyboard()
    {
        // Arrange
        var context = new EditContext(_model);

        // Act
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, input => input
            .Add(item => item.Culture, CultureInfo.InvariantCulture)
            .Add(item => item.MayBeNegative, true));

        // Assert
        cut.Find("input").HasAttribute("inputmode").ShouldBeFalse();
    }

    [Fact]
    public async Task A_new_model_value_replaces_refused_text()
    {
        // Arrange
        _model.Price = 2.125m;
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Price, value => _model.Price = value, Invariant);
        await cut.Type("big");
        var refused = cut.Find("input").GetAttribute("value");

        // Act
        cut.FindComponent<NumberTextInput<decimal>>().Render(parameters => parameters.Add(item => item.Value, 1.5m));

        // Assert
        (refused, cut.Find("input").GetAttribute("value")).ShouldBe(("big", "1.5"));
    }

    [Fact]
    public async Task A_message_is_filed_under_a_named_field_without_a_value_expression()
    {
        // Arrange
        var context = new EditContext(new object());
        var field = context.Field("prices[0]");
        var cut = this.RenderNamedNumberField(context, field);
        await cut.Type("1.250,50");

        // Act
        var valid = await cut.Submit(context);

        // Assert
        (valid, context.GetValidationMessages(field).ShouldHaveSingleItem()).ShouldBe((false, Message));
        cut.Find($"#{InputId}").GetAttribute("aria-describedby").ShouldBe($"{InputId}-error");
    }

    [Fact]
    public async Task Disposing_the_input_removes_its_message_so_the_form_validates()
    {
        // Arrange
        var context = new EditContext(_model);
        var cut = this.RenderNumberField(context, () => _model.Quantity, value => _model.Quantity = value, Invariant);
        await cut.Type("many");
        var invalid = await cut.Submit(context);

        // Act
        await DisposeComponentsAsync();
        var valid = await context.ValidateAsync(TestContext.Current.CancellationToken);

        // Assert
        (invalid, valid).ShouldBe((false, true));
    }

    [Fact]
    public void An_input_outside_a_form_without_a_field_or_message_or_of_another_type_throws()
    {
        // Arrange
        var context = new EditContext(_model);

        // Act
        var outsideForm = () => Render<NumberTextInput<int>>(parameters => parameters.Add(item => item.Message, Message));
        var withoutField = () => Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, context)
            .AddChildContent<NumberTextInput<int>>(input => input.Add(item => item.Message, Message)));
        var withoutMessage = () => Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, context)
            .AddChildContent<NumberTextInput<int>>(input => input.Add(item => item.ValueExpression, () => _model.Quantity)));
        var text = () => this.RenderNumberField(context, () => _model.Text, value => _model.Text = value);

        // Assert
        outsideForm.ShouldThrow<InvalidOperationException>();
        withoutField.ShouldThrow<InvalidOperationException>();
        withoutMessage.ShouldThrow<InvalidOperationException>();
        text.ShouldThrow<NotSupportedException>();
    }
}
