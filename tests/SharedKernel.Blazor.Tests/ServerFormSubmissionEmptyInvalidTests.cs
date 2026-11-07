using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>
/// Tests that a failed save never ends with nothing shown. The Results library cannot build an invalid result without field
/// messages, so these tests drive the decision with the status and the applied server errors that such a result would produce.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerFormSubmissionEmptyInvalidTests
{
    private const string FailureMessage = "We could not save. Try again later.";
    private const string ConflictMessage = "Someone else changed this. Reload and try again.";

    private readonly EditContext _context = new(new ServerValidationModel());

    [Fact]
    public void An_invalid_result_with_null_validation_errors_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);
        using var binder = new ServerValidationBinder(_context, ServerValidationFixtures.Options());
        var placed = binder.Apply((IReadOnlyDictionary<string, string[]>?)null);
        var summary = placed.ToSummaryItems(field => field);

        // Act
        var error = submission.ErrorFor(ResultStatus.Invalid, false, placed.HasUnplacedErrors, summary.Count);

        // Assert
        summary.Count.ShouldBe(0);
        error.ShouldBe(FailureMessage);
    }

    [Fact]
    public void An_invalid_result_with_an_empty_dictionary_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);
        using var binder = new ServerValidationBinder(_context, ServerValidationFixtures.Options());
        var placed = binder.Apply(new Dictionary<string, string[]>());
        var summary = placed.ToSummaryItems(field => field);

        // Act
        var error = submission.ErrorFor(ResultStatus.Invalid, false, placed.HasUnplacedErrors, summary.Count);

        // Assert
        summary.Count.ShouldBe(0);
        error.ShouldBe(FailureMessage);
    }

    [Fact]
    public void An_invalid_result_whose_messages_are_all_skipped_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);
        using var binder = new ServerValidationBinder(_context, ServerValidationFixtures.Options());
        var placed = binder.Apply(new Dictionary<string, string[]> { ["Email"] = [], ["Name"] = null! });
        var summary = placed.ToSummaryItems(field => field);

        // Act
        var error = submission.ErrorFor(ResultStatus.Invalid, false, placed.HasUnplacedErrors, summary.Count);

        // Assert
        summary.Count.ShouldBe(0);
        error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task An_invalid_result_on_an_unknown_field_shows_the_failure_message()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);

        // Act
        await submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Other", "Not shown.")));

        // Assert
        submission.Summary.Count.ShouldBe(0);
        submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task An_invalid_result_with_a_placed_message_shows_no_general_error()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);

        // Act
        await submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Email", "Email is required.")));

        // Assert
        submission.Error.ShouldBeNull();
        submission.Summary.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(ResultStatus.Conflict, ConflictMessage)]
    [InlineData(ResultStatus.Error, FailureMessage)]
    public void Non_invalid_failures_keep_their_messages_when_the_summary_is_empty(ResultStatus status, string expected)
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);

        // Act
        var error = submission.ErrorFor(status, false, false, 0);

        // Assert
        error.ShouldBe(expected);
    }

    [Fact]
    public void A_successful_save_shows_no_general_error_with_an_empty_summary()
    {
        // Arrange
        using var submission = ServerFormSubmissionFixtures.Create(_context, FailureMessage, ConflictMessage, null);

        // Act
        var error = submission.ErrorFor(ResultStatus.Ok, true, false, 0);

        // Assert
        error.ShouldBeNull();
    }
}
