using Microsoft.AspNetCore.Components;
using static PandaBear.Blazor.Tests.ComponentFixtures;
using static PandaBear.Blazor.Tests.UploadStatusRendering;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the accessible upload status component.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UploadStatusTests : BunitContext
{
    [Fact]
    public void Nothing_is_rendered_while_no_upload_is_displayed()
    {
        // Arrange & Act
        var cut = this.RenderStatus(new UploadOperationState());

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Region_is_labelled_and_contains_a_polite_atomic_status_with_the_announcement()
    {
        // Arrange
        var state = Started();

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        var region = cut.Find("section");
        region.GetAttribute("aria-label").ShouldBe(RegionLabel);
        region.GetAttribute("tabindex").ShouldBe("-1");
        var status = cut.Find("[role=status]");
        status.GetAttribute("aria-live").ShouldBe("polite");
        status.GetAttribute("aria-atomic").ShouldBe("true");
        status.TextContent.ShouldBe("customers.csv queued.");
    }

    [Fact]
    public void Announcement_stays_unchanged_for_progress_inside_one_announcement_step()
    {
        // Arrange
        var state = Started();
        var operationId = state.OperationId;
        state.ReportBytes(operationId, 10);
        var cut = this.RenderStatus(state);
        var before = cut.Find("[role=status]").TextContent;

        // Act
        state.ReportBytes(operationId, 15);
        cut.Render();

        // Assert
        before.ShouldBe("customers.csv upload is 10% complete.");
        cut.Find("[role=status]").TextContent.ShouldBe(before);
        cut.Find("progress").GetAttribute("value").ShouldBe("15");
    }

    [Fact]
    public void Progress_is_determinate_only_while_uploading()
    {
        // Arrange
        var state = Started();
        var operationId = state.OperationId;
        var cut = this.RenderStatus(state);

        // Assert
        cut.Find("progress").HasAttribute("value").ShouldBeFalse();

        // Act
        state.ReportBytes(operationId, 40);
        cut.Render();

        // Assert
        var determinate = cut.Find("progress");
        determinate.GetAttribute("value").ShouldBe("40");
        determinate.GetAttribute("max").ShouldBe("100");
        determinate.GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");

        // Act
        state.ReportBytes(operationId, 100);
        cut.Render();

        // Assert
        cut.Find("progress").HasAttribute("value").ShouldBeFalse();

        // Act
        state.Complete(operationId);
        cut.Render();

        // Assert
        cut.FindAll("progress").ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_button_is_named_after_the_file_raises_the_callback_and_exists_only_while_active()
    {
        // Arrange
        var canceled = 0;
        var state = Started();
        var cut = this.RenderStatus(state, () => canceled++);

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert
        button.GetAttribute("type").ShouldBe("button");
        button.GetAttribute("aria-label").ShouldBe("Cancel upload: customers.csv");
        button.TextContent.Trim().ShouldBe("Cancel upload");
        canceled.ShouldBe(1);

        // Act
        state.Cancel(state.OperationId);
        cut.Render();

        // Assert
        cut.FindAll("button").ShouldBeEmpty();
    }

    [Fact]
    public void Hostile_and_overlong_file_names_are_encoded_and_bounded()
    {
        // Arrange
        var state = Started(HostileText.Markup + HostileText.VeryLong(1_000));

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        cut.FindAll("script, img").ShouldBeEmpty();
        cut.Find("button").GetAttribute("aria-label")!.Length.ShouldBeLessThanOrEqualTo("Cancel upload: ".Length + UploadStatus.MaxDisplayTextLength);
    }

    [Fact]
    public void Failure_message_is_shown_encoded_and_bounded()
    {
        // Arrange
        var state = Started();
        state.Fail(state.OperationId, HostileText.Markup + HostileText.VeryLong(10_000));

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        cut.FindAll("script, img").ShouldBeEmpty();
        cut.Find("[data-upload-failure]").TextContent.Length.ShouldBeLessThanOrEqualTo(UploadStatus.MaxDisplayTextLength);
    }

    [Fact]
    public void Focus_moves_to_the_region_when_the_focused_cancel_button_disappears()
    {
        // Arrange
        var state = Started();
        var cut = this.RenderStatus(state);
        var regionReference = cut.Find("section").GetAttribute("blazor:elementreference");
        cut.Find("button").Focus();

        // Act
        state.Cancel(state.OperationId);
        cut.Render();

        // Assert
        var invocation = JSInterop.VerifyFocusAsyncInvoke(1)[0];
        var target = invocation.Arguments[0].ShouldBeOfType<ElementReference>();
        target.Id.ShouldBe(regionReference);
    }

    [Fact]
    public void Focus_is_left_alone_when_the_cancel_button_was_not_focused()
    {
        // Arrange
        var state = Started();
        var cut = this.RenderStatus(state);

        // Act
        state.Cancel(state.OperationId);
        cut.Render();

        // Assert
        JSInterop.VerifyNotInvoke("Blazor._internal.domWrapper.focus");
    }
}
