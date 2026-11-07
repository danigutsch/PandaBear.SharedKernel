using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests which fixed message a form shows for each kind of failed save and for client messages it cannot list.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerFormSubmissionOutcomeTests
{
    private const string FailureMessage = "We could not save. Try again later.";
    private const string ConflictMessage = "Someone else changed this. Reload and try again.";
    private const string NotFoundMessage = "This record no longer exists.";
    private const string ServerText = "secret server detail";

    private readonly EditContext _context = new(new ServerValidationModel());

    [Fact]
    public async Task A_conflict_shows_the_conflict_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);

        // Act
        var saved = await submission.Submit(() => Task.FromResult(Result.Conflict(ServerText)));

        // Assert
        saved.ShouldBeFalse();
        submission.Error.ShouldBe(ConflictMessage);
        submission.Summary.Count.ShouldBe(0);
    }

    [Fact]
    public async Task A_not_found_result_shows_the_not_found_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);

        // Act
        var saved = await submission.Submit(() => Task.FromResult(Result.NotFound(ServerText)));

        // Assert
        saved.ShouldBeFalse();
        submission.Error.ShouldBe(NotFoundMessage);
    }

    [Fact]
    public async Task A_conflict_without_a_conflict_message_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, null, null);

        // Act
        await submission.Submit(() => Task.FromResult(Result.Conflict(ServerText)));

        // Assert
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task A_not_found_result_without_a_not_found_message_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);

        // Act
        await submission.Submit(() => Task.FromResult(Result.NotFound(ServerText)));

        // Assert
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task Other_failures_show_the_failure_message_even_when_special_messages_are_set()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);

        // Act
        await submission.Submit(() => Task.FromResult(Result.Error(ServerText)));
        var afterError = submission.Error;
        await submission.Submit(() => Task.FromResult(Result.Forbidden(ServerText)));

        // Assert
        afterError.ShouldBe(FailureMessage);
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task Placed_field_errors_show_no_general_error_even_when_special_messages_are_set()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);

        // Act
        await submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Email", "Email is required.")));

        // Assert
        submission.Error.ShouldBeNull();
        submission.Summary.Select(item => item.TargetId).ShouldBe(["form-Email"]);
    }

    [Fact]
    public async Task A_message_on_an_unknown_field_blocks_the_save_and_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);
        var store = new ValidationMessageStore(_context);
        store.Add(_context.Field("Other"), ServerText);
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
        submission.Summary.Count.ShouldBe(0);
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task A_model_level_message_blocks_the_save_and_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, null, null);
        var store = new ValidationMessageStore(_context);
        store.Add(new FieldIdentifier(_context.Model, string.Empty), ServerText);

        // Act
        var saved = await submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeFalse();
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task A_message_on_a_known_field_blocks_the_save_with_a_summary_and_no_general_error()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, NotFoundMessage);
        var store = new ValidationMessageStore(_context);
        store.Add(_context.Field("Email"), "Enter a valid email.");
        store.Add(_context.Field("Other"), ServerText);

        // Act
        var saved = await submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeFalse();
        submission.Error.ShouldBeNull();
        submission.Summary.ShouldBe([new ErrorSummaryItem("form-Email", "Enter a valid email.")]);
    }
}
