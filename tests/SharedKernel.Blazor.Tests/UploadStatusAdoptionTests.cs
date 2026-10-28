using static PandaBear.Blazor.Tests.ComponentFixtures;
using static PandaBear.Blazor.Tests.UploadStatusRendering;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the parameters that let a consumer reproduce its own accessible names, classes, and live region content.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UploadStatusAdoptionTests : BunitContext
{
    [Fact]
    public void Defaults_render_the_existing_markup_for_a_determinate_upload()
    {
        // Arrange
        var state = Uploading();

        // Act
        var cut = this.RenderStatus(state);

        // Assert
        AttributeNames(cut.Find("[role=status]")).ShouldBe(["role"]);
        AttributeNames(cut.Find("progress")).ShouldBe(["aria-label", "max", "value"]);
        AttributeNames(cut.Find("button")).Order(StringComparer.Ordinal).ToArray().ShouldBe(["aria-label", "blazor:onblur", "blazor:onclick", "blazor:onfocus", "type"]);
        cut.Find("progress").TextContent.ShouldBe("40 / 100");
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");
        cut.Find("button").GetAttribute("aria-label").ShouldBe("Cancel upload: customers.csv");
        cut.Find("button").TextContent.Trim().ShouldBe("Cancel upload");
    }

    [Fact]
    public void Defaults_render_the_existing_markup_for_an_indeterminate_upload_and_a_failure()
    {
        // Arrange
        var queued = Started();
        var failed = Started();
        failed.Fail(failed.OperationId, "Too large.");

        // Act
        var queuedCut = this.RenderStatus(queued);
        var failedCut = this.RenderStatus(failed);

        // Assert
        AttributeNames(queuedCut.Find("progress")).ShouldBe(["aria-label"]);
        queuedCut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");
        failedCut.Find("[role=status] [data-upload-failure]").TextContent.ShouldBe("Too large.");
    }

    [Fact]
    public void Cancel_label_format_changes_only_the_accessible_name()
    {
        // Arrange
        var state = Started();

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.CancelLabelFormat, "Cancel upload of {0}"));

        // Assert
        cut.Find("button").GetAttribute("aria-label").ShouldBe("Cancel upload of customers.csv");
        cut.Find("button").TextContent.Trim().ShouldBe("Cancel upload");
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");
    }

    [Fact]
    public void Determinate_and_indeterminate_progress_labels_differ()
    {
        // Arrange
        var state = Started();
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.ProgressLabelFormat, "Upload progress for {0}")
            .Add(component => component.IndeterminateProgressLabelFormat, "Indeterminate server progress for {0}"));

        // Assert
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Indeterminate server progress for customers.csv");

        // Act
        state.ReportBytes(state.OperationId, 40);
        cut.Render();

        // Assert
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress for customers.csv");
    }

    [Fact]
    public void Indeterminate_format_alone_leaves_the_determinate_label_unchanged()
    {
        // Arrange
        var state = Uploading();

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.IndeterminateProgressLabelFormat, "Indeterminate for {0}"));

        // Assert
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");
    }

    [Fact]
    public void Progress_label_format_alone_applies_to_both_kinds_of_progress()
    {
        // Arrange
        var state = Started();
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.ProgressLabelFormat, "Progress of {0}"));

        // Assert
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Progress of customers.csv");

        // Act
        state.ReportBytes(state.OperationId, 40);
        cut.Render();

        // Assert
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Progress of customers.csv");
    }

    [Fact]
    public void Progress_text_format_changes_only_the_determinate_fallback_content()
    {
        // Arrange
        var state = Uploading();

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.ProgressTextFormat, "{0} of {1} bytes"));

        // Assert
        cut.Find("progress").TextContent.ShouldBe("40 of 100 bytes");
        cut.Find("progress").GetAttribute("aria-label").ShouldBe("Upload progress: customers.csv");
    }

    [Fact]
    public void Formatted_labels_receive_the_bounded_file_name()
    {
        // Arrange
        var state = Started(HostileText.Markup + HostileText.VeryLong(1_000) + "‮​");

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.CancelLabelFormat, "Cancel upload of {0}")
            .Add(component => component.IndeterminateProgressLabelFormat, "Indeterminate for {0}"));

        // Assert
        cut.FindAll("script, img").ShouldBeEmpty();
        cut.Find("button").GetAttribute("aria-label")!.Length.ShouldBeLessThanOrEqualTo("Cancel upload of ".Length + UploadStatus.MaxDisplayTextLength);
        cut.Find("progress").GetAttribute("aria-label")!.Length.ShouldBeLessThanOrEqualTo("Indeterminate for ".Length + UploadStatus.MaxDisplayTextLength);
        cut.Markup.ShouldNotContain("‮", StringComparison.Ordinal);
        cut.Markup.ShouldNotContain("​", StringComparison.Ordinal);
    }

    [Fact]
    public void Classes_land_on_the_status_progress_and_cancel_elements_only()
    {
        // Arrange
        var state = Uploading();

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.StatusClass, "visually-hidden")
            .Add(component => component.ProgressClass, "w-100 mt-2")
            .Add(component => component.CancelClass, "btn btn-sm btn-outline-danger mt-2"));

        // Assert
        cut.Find("[role=status]").GetAttribute("class").ShouldBe("visually-hidden");
        cut.Find("progress").GetAttribute("class").ShouldBe("w-100 mt-2");
        cut.Find("button").GetAttribute("class").ShouldBe("btn btn-sm btn-outline-danger mt-2");
        cut.Find("section").HasAttribute("class").ShouldBeFalse();
    }

    [Fact]
    public void Each_class_parameter_affects_only_its_own_element()
    {
        // Arrange
        var state = Uploading();

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.CancelClass, "only-cancel"));

        // Assert
        cut.Find("button").GetAttribute("class").ShouldBe("only-cancel");
        cut.Find("progress").HasAttribute("class").ShouldBeFalse();
        cut.Find("[role=status]").HasAttribute("class").ShouldBeFalse();
    }

    [Fact]
    public void Failure_message_is_kept_out_of_the_live_region_when_disabled()
    {
        // Arrange
        var state = Started();
        state.Fail(state.OperationId, "The file is too large.");

        // Act
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.RenderFailureInStatus, false)
            .Add(component => component.Detail, TextDetail(current => current.FailureMessage ?? string.Empty)));

        // Assert
        cut.Find("[role=status]").TextContent.ShouldBe("customers.csv failed.");
        cut.FindAll("[data-upload-failure]").ShouldBeEmpty();
        cut.Find("[data-upload-detail]").TextContent.ShouldBe("The file is too large.");
    }

    [Fact]
    public void Focus_still_moves_to_the_region_when_the_cancel_button_disappears_with_custom_parameters()
    {
        // Arrange
        var state = Started();
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, RegionLabel)
            .Add(component => component.CancelClass, "btn")
            .Add(component => component.CancelLabelFormat, "Cancel {0}"));
        cut.Find("button").Focus();

        // Act
        state.Cancel(state.OperationId);
        cut.Render();

        // Assert
        JSInterop.VerifyFocusAsyncInvoke(1);
    }
}
