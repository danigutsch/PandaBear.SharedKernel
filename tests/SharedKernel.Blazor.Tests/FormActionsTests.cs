using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Tests;

/// <summary>Tests the end of a form: the save error, the submit button that is disabled while saving, extra buttons and the cancel link.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FormActionsTests : BunitContext
{
    [Fact]
    public void A_fresh_form_shows_an_enabled_save_button_and_no_error_or_cancel_link()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "tour", "tour");

        // Act
        var cut = Render<FormActions>(parameters => parameters.Add(component => component.Submission, submission));

        // Assert
        var button = cut.Find("button");
        button.GetAttribute("type").ShouldBe("submit");
        button.TextContent.ShouldBe("Save");
        button.HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[role=alert]").ShouldBeEmpty();
        cut.FindAll("a").ShouldBeEmpty();
    }

    [Fact]
    public void The_texts_and_the_cancel_link_can_be_set_and_extra_buttons_sit_between_save_and_cancel()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "image", "image");

        // Act
        var cut = Render<FormActions>(parameters => parameters
            .Add(component => component.Submission, submission)
            .Add(component => component.SubmitText, "Upload")
            .Add(component => component.CancelHref, "site/images")
            .Add(component => component.CancelText, "Back")
            .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button type=\"button\">Preview</button>"))));

        // Assert
        var controls = cut.FindAll("button, a");
        controls.Select(static control => control.TextContent).ShouldBe(["Upload", "Preview", "Back"]);
        cut.Find("a").GetAttribute("href").ShouldBe("site/images");
    }

    [Fact]
    public async Task A_failed_save_shows_its_message_as_an_alert()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "tour", "tour");
        var cut = Render<FormActions>(parameters => parameters.Add(component => component.Submission, submission));

        // Act
        await submission.Submit(() => Task.FromResult(FailedResults.For(ResultStatus.Conflict)));
        cut.Render();

        // Assert
        cut.Find("p[role=alert]").TextContent.ShouldBe("The tour was changed or removed by someone else. Reload the page and try again.");
    }

    [Fact]
    public async Task The_save_button_is_disabled_while_saving()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "tour", "tour");
        var cut = Render<FormActions>(parameters => parameters.Add(component => component.Submission, submission));
        var pending = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var saving = submission.Submit(() => pending.Task);
        cut.Render();
        var disabledWhileSaving = cut.Find("button").HasAttribute("disabled");
        pending.SetResult(Result.Ok());
        await saving;
        cut.Render();

        // Assert
        disabledWhileSaving.ShouldBeTrue();
        cut.Find("button").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void A_missing_submission_is_refused()
    {
        // Act
        Action act = () => Render<FormActions>();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }
}
