using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the label, control and validation message of one form field.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
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
        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(ServerValidationModel.Name)), "Name is required.");
        context.NotifyValidationStateChanged();

        // Act
        var cut = this.RenderField(context, _model);

        // Assert
        cut.Find(".validation-message").TextContent.ShouldBe("Name is required.");
    }
}
