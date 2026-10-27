using Microsoft.AspNetCore.Components.Forms;
using static PandaBear.Blazor.Tests.ServerValidationFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests applying server validation errors to an <see cref="EditContext" />.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerValidationBinderTests
{
    [Fact]
    public void Known_fields_are_stored_per_field_and_matched_without_regard_to_case()
    {
        // Arrange
        var model = new ServerValidationModel();
        var context = new EditContext(model);
        using var binder = new ServerValidationBinder(context, Options());

        // Act
        var result = binder.Apply(Errors(("name", ["Name is required."])));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldBeEquivalentTo("Name is required.");
        result.HasUnplacedErrors.ShouldBeFalse();
        result.Issues.ShouldHaveSingleItem().FieldName.ShouldBe("Name");
    }

    [Fact]
    public void Unknown_and_model_level_keys_are_unplaced_and_never_stored()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());

        // Act
        var result = binder.Apply(Errors(("", ["Model level."]), ("Nope", ["Unknown."])));

        // Assert
        result.HasUnplacedErrors.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        context.GetValidationMessages().ShouldBeEmpty();
    }

    [Fact]
    public void Overlong_field_names_are_unplaced()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());

        // Act
        var result = binder.Apply(Errors((HostileText.VeryLong(10_000), ["Bad."])));

        // Assert
        result.HasUnplacedErrors.ShouldBeTrue();
        context.GetValidationMessages().ShouldBeEmpty();
    }

    [Fact]
    public void Overlong_messages_are_truncated_to_the_bound()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var options = Options() with
        {
            MaxMessageLength = 40
        };
        using var binder = new ServerValidationBinder(context, options);

        // Act
        binder.Apply(Errors(("Name", [HostileText.VeryLong(10_000)])));

        // Assert
        var stored = context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldHaveSingleItem();
        stored.Length.ShouldBeLessThanOrEqualTo(40);
    }

    [Theory]
    [InlineData(HostileText.StackTrace)]
    [InlineData(HostileText.MultilineStackTrace)]
    public void Stack_trace_like_messages_are_replaced_by_the_fallback_message(string hostile)
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());

        // Act
        var result = binder.Apply(Errors(("Name", [hostile])));

        // Assert
        var stored = context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldHaveSingleItem();
        stored.ShouldBe("This value is not valid.");
        result.Issues.ShouldHaveSingleItem().Message.ShouldBe("This value is not valid.");
    }

    [Fact]
    public void Message_and_field_counts_are_bounded()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var options = Options() with
        {
            MaxMessagesPerField = 2
        };
        using var binder = new ServerValidationBinder(context, options);

        // Act
        var result = binder.Apply(Errors(("Name", ["a", "b", "c", "d"])));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).Count().ShouldBe(2);
        result.Issues.ShouldHaveCount(2);
    }

    [Fact]
    public void Changing_a_field_clears_only_that_fields_server_messages()
    {
        // Arrange
        var model = new ServerValidationModel();
        var context = new EditContext(model);
        using var binder = new ServerValidationBinder(context, Options());
        binder.Apply(Errors(("Name", ["Name taken."]), ("Email", ["Email taken."])));

        // Act
        context.NotifyFieldChanged(context.Field(nameof(ServerValidationModel.Name)));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldBeEmpty();
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Email))).ShouldBeEquivalentTo("Email taken.");
    }

    [Fact]
    public void Apply_replaces_earlier_messages_and_clear_removes_them()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());
        binder.Apply(Errors(("Name", ["First."])));

        // Act
        binder.Apply(Errors(("Email", ["Second."])));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldBeEmpty();
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Email))).ShouldBeEquivalentTo("Second.");

        // Act
        binder.Clear();

        // Assert
        context.GetValidationMessages().ShouldBeEmpty();
    }

    [Fact]
    public void Disposed_binder_no_longer_reacts_to_field_changes()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var binder = new ServerValidationBinder(context, Options());
        binder.Apply(Errors(("Name", ["Name taken."])));
        binder.Dispose();

        // Act
        context.NotifyFieldChanged(context.Field(nameof(ServerValidationModel.Name)));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldBeEquivalentTo("Name taken.");
    }

    [Fact]
    public void Validation_outcome_errors_are_applied_and_missing_errors_are_empty()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());
        var outcome = new HttpOutcome
        {
            Kind = HttpOutcomeKind.ValidationProblem,
            ValidationErrors = Errors(("Name", ["Name is required."])),
        };

        // Act
        var withErrors = binder.Apply(outcome);
        var withoutErrors = binder.Apply(new HttpOutcome { Kind = HttpOutcomeKind.ValidationProblem });

        // Assert
        withErrors.Issues.ShouldHaveSingleItem();
        withoutErrors.Issues.ShouldBeEmpty();
        withoutErrors.HasUnplacedErrors.ShouldBeFalse();
        context.GetValidationMessages().ShouldBeEmpty();
    }

    [Fact]
    public void Summary_items_link_each_issue_to_the_field_id_chosen_by_the_caller()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());
        var result = binder.Apply(Errors(("Email", ["Email taken."])));

        // Act
        var items = result.ToSummaryItems(fieldName => "input-" + fieldName);

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.TargetId.ShouldBe("input-Email");
        item.Message.ShouldBe("Email taken.");
    }

    [Fact]
    public void Identical_messages_for_one_field_are_shown_once()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());

        // Act
        var result = binder.Apply(Errors(("Name", ["Taken.", "Taken.", "Taken."])));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).ShouldHaveSingleItem();
        result.Issues.ShouldHaveSingleItem();
    }

    [Fact]
    public void Message_cap_applies_across_case_variant_keys_and_reports_dropped_messages()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var options = Options() with
        {
            MaxMessagesPerField = 2
        };
        using var binder = new ServerValidationBinder(context, options);

        // Act
        var result = binder.Apply(Errors(("name", ["a", "b"]), ("NAME", ["c", "d"])));

        // Assert
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Name))).Count().ShouldBe(2);
        result.HasUnplacedErrors.ShouldBeTrue();
    }

    [Fact]
    public void Messages_dropped_by_the_per_field_cap_set_the_unplaced_flag()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var options = Options() with
        {
            MaxMessagesPerField = 2
        };
        using var binder = new ServerValidationBinder(context, options);

        // Act
        var result = binder.Apply(Errors(("Name", ["a", "b", "c"])));

        // Assert
        result.HasUnplacedErrors.ShouldBeTrue();
    }

    [Fact]
    public void Keys_beyond_the_field_cap_are_unplaced_even_when_known()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        var options = Options() with
        {
            MaxFields = 1
        };
        using var binder = new ServerValidationBinder(context, options);

        // Act
        var result = binder.Apply(Errors(("Name", ["a"]), ("Email", ["b"])));

        // Assert
        result.HasUnplacedErrors.ShouldBeTrue();
        result.Issues.ShouldHaveSingleItem().FieldName.ShouldBe("Name");
        context.GetValidationMessages(context.Field(nameof(ServerValidationModel.Email))).ShouldBeEmpty();
    }

    [Fact]
    public void A_new_validation_request_clears_server_messages_so_unchanged_data_can_be_resubmitted()
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());
        using var binder = new ServerValidationBinder(context, Options());
        binder.Apply(Errors(("Name", ["Name taken."])));

        // Act
        var valid = context.Validate();

        // Assert
        valid.ShouldBeTrue();
        context.GetValidationMessages().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_bounds_are_rejected(int bound)
    {
        // Arrange
        var options = Options();

        // Act
        var length = () => options with { MaxMessageLength = bound };
        var name = () => options with { MaxFieldNameLength = bound };
        var perField = () => options with { MaxMessagesPerField = bound };
        var fields = () => options with { MaxFields = bound };

        // Assert
        length.ShouldThrow<ArgumentOutOfRangeException>();
        name.ShouldThrow<ArgumentOutOfRangeException>();
        perField.ShouldThrow<ArgumentOutOfRangeException>();
        fields.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
