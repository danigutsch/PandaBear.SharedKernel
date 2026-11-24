using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests server-named fields, group messages and links to the controls.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class NamedFormFieldTests : BunitContext
{
    [Fact]
    public async Task A_nested_key_keeps_its_inline_label_help_and_message_IDs()
    {
        // Arrange
        var context = new EditContext(new object());
        var field = context.Field("pt.sections[0].heading");
        var store = new ValidationMessageStore(context);
        var cut = Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, context)
            .AddChildContent<NamedFormField>(item => item
                .Add(value => value.Field, field)
                .Add(value => value.InputId, "heading-input")
                .Add(value => value.Label, "Heading")
                .Add(value => value.Help, "Shown at the top of the section.")
                .Add(value => value.Inline, true)
                .Add(value => value.ChildContent, HeadingInput)));
        var validDescription = cut.Find("#heading-input").GetAttribute("aria-describedby");

        // Act
        await cut.InvokeAsync(() =>
        {
            store.Add(context.Field("pt.sections[1].heading"), "Another section.");
            store.Add(field, ["Enter a heading.", "Keep it short."]);
            context.NotifyValidationStateChanged();
        });

        // Assert
        validDescription.ShouldBe("heading-input-help");
        cut.Find("p > input + label.inline").GetAttribute("for").ShouldBe("heading-input");
        cut.Find("#heading-input").GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find("#heading-input").GetAttribute("aria-describedby").ShouldBe("heading-input-help heading-input-error heading-input-error-2");
        cut.Find("#heading-input-help").TextContent.ShouldBe("Shown at the top of the section.");
        cut.Find("#heading-input-error").TextContent.ShouldBe("Enter a heading.");
        cut.Find("#heading-input-error-2").TextContent.ShouldBe("Keep it short.");
        cut.FindAll(".validation-message").Count.ShouldBe(2);
    }

    [Fact]
    public void A_message_only_group_renders_its_message_without_a_control_or_label()
    {
        // Arrange
        var context = new EditContext(new object());
        var field = context.Field("pt.sections");
        var store = new ValidationMessageStore(context);
        store.Add(field, "Add a section.");

        // Act
        var cut = Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, context)
            .AddChildContent<NamedFormField>(item => item.Add(value => value.Field, field)));

        // Assert
        cut.MarkupMatches("<div class=\"validation-message\">Add a section.</div>");
    }

    [Fact]
    public void The_summary_keeps_the_supplied_focus_targets_and_field_order()
    {
        // Arrange
        var context = new EditContext(new object());
        var title = context.Field("pt.title");
        var heading = context.Field("pt.sections[0].heading");
        var capacity = context.Field("capacity");
        var store = new ValidationMessageStore(context);
        store.Add(heading, ["Enter a heading.", "Keep it short."]);
        store.Add(title, "Enter a title.");

        // Act
        var items = EditContextErrorSummary.Build(context, [(title, "title-input"), (capacity, "capacity-input"), (heading, "heading-input")]);
        var summary = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "Check these fields")
            .Add(component => component.Items, items));

        // Assert
        summary.FindAll("a").Select(link => link.GetAttribute("href")).ShouldBe(["http://localhost/#title-input", "http://localhost/#heading-input", "http://localhost/#heading-input"]);
        summary.FindAll("a").Select(link => link.GetAttribute("target")).ShouldBe(["_top", "_top", "_top"]);
        JSInterop.VerifyFocusAsyncInvoke(1);
        summary.FindAll("a").Select(link => link.TextContent).ShouldBe(["Enter a title.", "Enter a heading.", "Keep it short."]);
    }

    private static RenderFragment<IReadOnlyDictionary<string, object>> HeadingInput => attributes => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "id", "heading-input");
        builder.AddMultipleAttributes(2, attributes);
        builder.CloseElement();
    };
}
