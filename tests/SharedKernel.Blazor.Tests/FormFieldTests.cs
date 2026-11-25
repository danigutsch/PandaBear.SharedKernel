using Microsoft.AspNetCore.Components.Forms;

namespace SharedKernel.Blazor.Tests;

/// <summary>Tests the label, control and validation message of one form field.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FormFieldTests : BunitContext
{
    private readonly ServerValidationModel _model = new();

    [Fact]
    public void The_label_points_at_the_control_and_the_control_is_rendered()
    {
        // Act
        var cut = this.RenderField(new EditContext(_model), _model);

        // Assert
        var label = cut.Find("label");
        label.TextContent.ShouldBe("Your name");
        label.GetAttribute("for").ShouldBe("name-input");
        cut.Find("input#name-input").ShouldNotBeNull();
    }

    [Fact]
    public void A_validation_message_for_the_field_is_shown_after_the_control()
    {
        // Arrange
        var context = new EditContext(_model);
        context.ShowNameMessages(new ValidationMessageStore(context), "Name is required.");

        // Act
        var cut = this.RenderField(context, _model);

        // Assert
        cut.Find(".validation-message").TextContent.ShouldBe("Name is required.");
    }

    [Fact]
    public void An_invalid_control_is_described_by_its_message()
    {
        // Arrange
        var context = new EditContext(_model);
        context.ShowNameMessages(new ValidationMessageStore(context), "Name is required.");

        // Act
        var cut = this.RenderField(context, _model);

        // Assert
        var input = cut.Find("input#name-input");
        input.GetAttribute("aria-invalid").ShouldBe("true");
        input.GetAttribute("aria-describedby").ShouldBe("name-input-error");
        cut.Find("#name-input-error").TextContent.ShouldBe("Name is required.");
    }

    [Fact]
    public void A_valid_control_has_no_description_and_no_invalid_state()
    {
        // Act
        var cut = this.RenderField(new EditContext(_model), _model);

        // Assert
        var input = cut.Find("input#name-input");
        input.HasAttribute("aria-describedby").ShouldBeFalse();
        input.HasAttribute("aria-invalid").ShouldBeFalse();
        cut.FindAll(".validation-message").ShouldBeEmpty();
    }

    [Fact]
    public void Every_message_has_its_own_ID_and_all_of_them_describe_the_control()
    {
        // Arrange
        var context = new EditContext(_model);
        context.ShowNameMessages(new ValidationMessageStore(context), "Name is required.", "Name is too short.");

        // Act
        var cut = this.RenderField(context, _model);

        // Assert
        cut.Find("input#name-input").GetAttribute("aria-describedby").ShouldBe("name-input-error name-input-error-2");
        cut.Find("#name-input-error").TextContent.ShouldBe("Name is required.");
        cut.Find("#name-input-error-2").TextContent.ShouldBe("Name is too short.");
    }

    [Fact]
    public async Task IDs_given_in_described_by_come_first_and_stay_while_the_field_is_valid()
    {
        // Arrange
        var context = new EditContext(_model);
        var store = new ValidationMessageStore(context);
        var cut = this.RenderField(context, _model, describedBy: "name-help");
        var valid = cut.Find("input#name-input").GetAttribute("aria-describedby");

        // Act
        await cut.InvokeAsync(() => context.ShowNameMessages(store, "Name is required."));

        // Assert
        valid.ShouldBe("name-help");
        cut.Find("input#name-input").GetAttribute("aria-describedby").ShouldBe("name-help name-input-error");
    }

    [Fact]
    public async Task The_description_follows_the_validation_state_of_the_field()
    {
        // Arrange
        var context = new EditContext(_model);
        var store = new ValidationMessageStore(context);
        var cut = this.RenderField(context, _model);

        // Act
        await cut.InvokeAsync(() => context.ShowNameMessages(store, "Name is required."));
        var invalid = cut.Find("input#name-input").GetAttribute("aria-describedby");
        await cut.InvokeAsync(() => context.ShowNameMessages(store));

        // Assert
        invalid.ShouldBe("name-input-error");
        cut.Find("input#name-input").HasAttribute("aria-describedby").ShouldBeFalse();
        cut.FindAll(".validation-message").ShouldBeEmpty();
    }

    [Fact]
    public void A_message_on_another_field_does_not_describe_the_control()
    {
        // Arrange
        var context = new EditContext(_model);
        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(ServerValidationModel.Email)), "Email is required.");
        context.NotifyValidationStateChanged();

        // Act
        var cut = this.RenderField(context, _model);

        // Assert
        cut.Find("input#name-input").HasAttribute("aria-describedby").ShouldBeFalse();
        cut.FindAll(".validation-message").ShouldBeEmpty();
    }

    [Fact]
    public void A_field_outside_an_edit_form_is_rejected()
    {
        // Act
        var render = () => Render<FormField<string>>(parameters => parameters
            .Add(item => item.Label, "Your name")
            .Add(item => item.InputId, "name-input")
            .Add(item => item.For, () => _model.Name));

        // Assert
        render.ShouldThrow<InvalidOperationException>();
    }
}
