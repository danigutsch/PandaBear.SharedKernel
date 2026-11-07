using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests that a value the input component cannot parse blocks the save and is reported in the summary.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerFormSubmissionParseErrorTests : BunitContext
{
    [Fact]
    public async Task An_unparsable_number_blocks_the_save_and_is_shown_in_the_summary()
    {
        // Arrange
        var model = new NumericFormModel();
        var editContext = new EditContext(model);
        using var submission = new ServerFormSubmission(editContext, new ServerValidationOptions(["Quantity"], "Not valid."), field => "form-" + field, "Failed.");
        var cut = this.RenderQuantityForm(editContext, model);
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "abc" });
        var saves = 0;

        // Act
        var saved = await submission.Submit(() =>
        {
            saves++;
            return Task.FromResult(Result.Ok());
        });

        // Assert
        saved.ShouldBeFalse();
        saves.ShouldBe(0);
        submission.IsSaving.ShouldBeFalse();
        submission.Error.ShouldBeNull();
        submission.Summary.Select(item => item.TargetId).ShouldBe(["form-Quantity"]);
        submission.Summary[0].Message.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_valid_number_after_the_unparsable_one_saves()
    {
        // Arrange
        var model = new NumericFormModel();
        var editContext = new EditContext(model);
        using var submission = new ServerFormSubmission(editContext, new ServerValidationOptions(["Quantity"], "Not valid."), field => "form-" + field, "Failed.");
        var cut = this.RenderQuantityForm(editContext, model);
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "abc" });
        await cut.InvokeAsync(() => submission.Submit(() => Task.FromResult(Result.Ok())));
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "5" });

        // Act
        var saved = await cut.InvokeAsync(() => submission.Submit(() => Task.FromResult(Result.Ok())));

        // Assert
        saved.ShouldBeTrue();
        model.Quantity.ShouldBe(5);
        submission.Summary.Count.ShouldBe(0);
    }
}
