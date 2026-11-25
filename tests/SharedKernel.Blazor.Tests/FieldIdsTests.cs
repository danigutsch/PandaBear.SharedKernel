using Microsoft.AspNetCore.Components.Forms;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Tests;

/// <summary>Tests the control ids of form fields, which stay valid and selector-safe for nested field paths.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FieldIdsTests
{
    [Theory]
    [InlineData("tour", "Name", "tour-Name")]
    [InlineData("site-tour", "pt.title", "site-tour-pt-title")]
    [InlineData("site-tour", "pt.itinerary[0].title", "site-tour-pt-itinerary-0-title")]
    [InlineData("site-page", "sections[12].items[3]", "site-page-sections-12-items-3")]
    public void A_field_ID_is_the_prefix_and_the_path_with_dots_and_indexes_as_hyphens(string prefix, string field, string expected)
    {
        // Act
        var id = FieldIds.For(prefix, field);

        // Assert
        id.ShouldBe(expected);
    }

    [Fact]
    public async Task A_record_form_links_a_nested_field_error_to_its_path_safe_control_ID()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["pt.itinerary[0].title"], "site-tour", "tour");
        var invalid = Result.Invalid("bad", "pt.itinerary[0].title", "Too long.");

        // Act
        await submission.Submit(() => Task.FromResult(invalid));

        // Assert
        submission.Summary.ShouldHaveSingleItem().TargetId.ShouldBe("site-tour-pt-itinerary-0-title");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_prefix_is_refused(string prefix)
    {
        // Act
        Action act = () => FieldIds.For(prefix, "Name");

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_missing_prefix_or_field_is_refused()
    {
        // Act
        Action withoutPrefix = () => FieldIds.For(null!, "Name");
        Action withoutField = () => FieldIds.For("tour", null!);

        // Assert
        withoutPrefix.ShouldThrow<ArgumentNullException>();
        withoutField.ShouldThrow<ArgumentNullException>();
    }
}
