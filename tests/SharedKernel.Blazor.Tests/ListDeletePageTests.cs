using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the keyboard focus of a list page whose inline delete confirmation opens, closes and deletes.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ListDeletePageTests : BunitContext
{
    private const string ReferenceAttribute = "blazor:elementreference";

    [Fact]
    public void Opening_the_confirmation_focuses_its_cancel_button()
    {
        // Arrange
        var cut = Render<ListDeletePage>();

        // Act
        cut.Find("#delete-1").Click();

        // Assert
        this.FocusedIds().ShouldBe([cut.Find("#keep-1").GetAttribute(ReferenceAttribute)!]);
    }

    [Fact]
    public void Cancelling_the_confirmation_focuses_the_delete_button_that_opened_it()
    {
        // Arrange
        var cut = Render<ListDeletePage>();
        cut.Find("#delete-1").Click();

        // Act
        cut.Find("#keep-1").Click();

        // Assert
        var opener = cut.Find("#delete-1").GetAttribute(ReferenceAttribute)!;
        this.FocusedIds()[^1].ShouldBe(opener);
        cut.FindAll("[role=status]").ShouldBeEmpty();
    }

    [Fact]
    public void A_deletion_is_announced_in_the_status_region_which_takes_focus()
    {
        // Arrange
        var cut = Render<ListDeletePage>();
        cut.Find("#delete-1").Click();

        // Act
        cut.Find("#confirm-1").Click();

        // Assert
        var status = cut.Find("[role=status]");
        status.TextContent.ShouldBe("Row 1 was deleted.");
        this.FocusedIds()[^1].ShouldBe(status.GetAttribute(ReferenceAttribute));
        cut.FindAll("#delete-1").ShouldBeEmpty();
    }

    [Fact]
    public void A_failed_deletion_focuses_the_delete_button_of_the_kept_row()
    {
        // Arrange
        var cut = Render<ListDeletePage>(parameters => parameters.Add(page => page.Outcome, Result.Error("boom")));
        cut.Find("#delete-2").Click();

        // Act
        cut.Find("#confirm-2").Click();

        // Assert
        this.FocusedIds()[^1].ShouldBe(cut.Find("#delete-2").GetAttribute(ReferenceAttribute));
        cut.FindAll("[role=status]").ShouldBeEmpty();
    }
}
