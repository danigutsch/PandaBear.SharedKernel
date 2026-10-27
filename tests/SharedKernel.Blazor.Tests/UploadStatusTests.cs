using Microsoft.AspNetCore.Components;
using static PandaBear.Blazor.Tests.ComponentFixtures;
using static PandaBear.Blazor.Tests.UploadStatusRendering;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the accessible upload status component.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UploadStatusTests : BunitContext
{
    [Fact]
    public void Labelled_region_and_empty_status_exist_before_the_first_state_change()
    {
        // Arrange & Act
        var cut = this.RenderStatus(new UploadOperationState());

        // Assert
        cut.Find("section").GetAttribute("aria-label").ShouldBe(RegionLabel);
        cut.Find("[role=status]").TextContent.ShouldBeEmpty();
        cut.FindAll("progress, button").ShouldBeEmpty();
    }

    [Fact]
    public void Status_carries_the_announcement_and_relies_on_the_implicit_live_region_semantics()
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
        status.HasAttribute("aria-live").ShouldBeFalse();
        status.HasAttribute("aria-atomic").ShouldBeFalse();
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
        var before = cut.Find("[role=status]").OuterHtml;

        // Act
        state.ReportBytes(operationId, 15);
        cut.Render();
        var inStep = cut.Find("[role=status]").OuterHtml;
        state.ReportBytes(operationId, 20);
        cut.Render();

        // Assert
        inStep.ShouldBe(before);
        cut.Find("[role=status]").TextContent.ShouldBe("customers.csv upload is 20% complete.");
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
        cut.Find("[role=status] [data-upload-failure]").TextContent.Length.ShouldBeLessThanOrEqualTo(UploadStatus.MaxDisplayTextLength);
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
        JSInterop.Invocations.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_reason_is_inside_the_live_status_region()
    {
        // Arrange
        var state = Started();
        state.Fail(state.OperationId, "The file is too large.");

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        var status = cut.Find("[role=status]");
        status.TextContent.ShouldContain("customers.csv failed.", StringComparison.Ordinal);
        status.TextContent.ShouldContain("The file is too large.", StringComparison.Ordinal);
    }

    [Fact]
    public void Bidi_and_zero_width_characters_cannot_spoof_the_file_name()
    {
        // Arrange
        var state = Started("invoice\u202Efdp.exe\u200B");

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        cut.Markup.ShouldNotContain("\u202E", StringComparison.Ordinal);
        cut.Markup.ShouldNotContain("\u200B", StringComparison.Ordinal);
        cut.Find("button").GetAttribute("aria-label").ShouldBe("Cancel upload: invoicefdp.exe");
    }

    [Fact]
    public void Focus_changes_on_the_cancel_button_do_not_re_render_the_component()
    {
        // Arrange
        var state = Started();
        var cut = this.RenderStatus(state);
        var renders = cut.RenderCount;

        // Act
        cut.Find("button").Focus();
        cut.Find("button").Blur();

        // Assert
        cut.RenderCount.ShouldBe(renders);
    }

    [Fact]
    public void Missing_state_is_rejected()
    {
        // Arrange & Act
        var render = () =>
        {
            Render<UploadStatus>(parameters => parameters
                .Add(component => component.State, null!)
                .Add(component => component.Label, RegionLabel));
        };

        // Assert
        render.ShouldThrow<ArgumentNullException>();
    }
}
