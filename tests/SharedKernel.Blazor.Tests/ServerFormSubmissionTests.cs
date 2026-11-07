using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the state a form keeps while it submits to a server and shows the outcome.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerFormSubmissionTests : IDisposable
{
    private const string FailureMessage = "We could not save. Try again later.";

    private readonly ServerValidationModel _model = new();
    private readonly EditContext _context;
    private readonly ServerFormSubmission _submission;

    /// <summary>Creates a form over a fresh model with two known fields.</summary>
    public ServerFormSubmissionTests()
    {
        _context = new EditContext(_model);
        _submission = new ServerFormSubmission(_context, new ServerValidationOptions(["Name", "Email"], "Not valid."), field => "form-" + field, FailureMessage);
    }

    [Fact]
    public async Task A_successful_save_returns_true_and_shows_nothing()
    {
        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeTrue();
        _submission.Error.ShouldBeNull();
        _submission.Summary.Count.ShouldBe(0);
        _submission.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task The_form_reports_saving_while_the_save_runs()
    {
        // Arrange
        var duringSave = false;

        // Act
        await _submission.Submit(() =>
        {
            duringSave = _submission.IsSaving;
            return Task.FromResult(Result.Ok());
        });

        // Assert
        duringSave.ShouldBeTrue();
        _submission.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task Field_errors_are_shown_on_the_fields_and_in_the_summary_without_a_general_error()
    {
        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Email", "Email is required.")));

        // Assert
        saved.ShouldBeFalse();
        _submission.Error.ShouldBeNull();
        _submission.Summary.Select(item => item.TargetId).ShouldBe(["form-Email"]);
        _context.GetValidationMessages(_context.Field("Email")).ShouldBe(["Email is required."]);
    }

    [Fact]
    public async Task A_field_error_that_cannot_be_placed_shows_the_general_error()
    {
        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Unknown", "Hidden.")));

        // Assert
        saved.ShouldBeFalse();
        _submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task A_failure_that_is_not_a_validation_failure_shows_the_general_error()
    {
        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.NotFound("missing")));

        // Assert
        saved.ShouldBeFalse();
        _submission.Error.ShouldBe(FailureMessage);
        _submission.Summary.Count.ShouldBe(0);
    }

    [Fact]
    public async Task A_later_successful_save_clears_the_previous_errors()
    {
        // Arrange
        await _submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Email", "Email is required.")));

        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeTrue();
        _submission.Error.ShouldBeNull();
        _submission.Summary.Count.ShouldBe(0);
        _context.GetValidationMessages().Count().ShouldBe(0);
    }

    [Fact]
    public async Task A_throwing_save_propagates_and_leaves_the_form_usable()
    {
        // Act
        Func<Task> act = () => _submission.Submit(() => throw new InvalidOperationException("boom"));

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
        _submission.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task A_second_submit_while_saving_returns_false_without_running_the_save()
    {
        // Arrange
        var saves = 0;
        var gate = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = _submission.Submit(() =>
        {
            saves++;
            return gate.Task;
        });

        // Act
        var second = await _submission.Submit(() =>
        {
            saves++;
            return Task.FromResult(Result.Ok());
        });
        gate.SetResult(Result.Ok());
        var firstSaved = await first;

        // Assert
        second.ShouldBeFalse();
        firstSaved.ShouldBeTrue();
        saves.ShouldBe(1);
        _submission.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task A_throwing_save_leaves_no_stale_errors_from_the_previous_save()
    {
        // Arrange
        await _submission.Submit(() => Task.FromResult(Result.Invalid("bad", "Email", "Email is required.")));

        // Act
        Func<Task> act = () => _submission.Submit(() => throw new InvalidOperationException("boom"));

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
        _submission.Summary.Count.ShouldBe(0);
        _submission.Error.ShouldBeNull();
        _submission.IsSaving.ShouldBeFalse();
        _context.GetValidationMessages().Count().ShouldBe(0);
    }

    [Fact]
    public async Task A_message_from_another_store_blocks_the_save_and_is_shown_in_the_summary()
    {
        // Arrange
        var store = new ValidationMessageStore(_context);
        store.Add(_context.Field("Email"), "Enter a valid email.");
        var saves = 0;

        // Act
        var saved = await _submission.Submit(() =>
        {
            saves++;
            return Task.FromResult(Result.Ok());
        });

        // Assert
        saved.ShouldBeFalse();
        saves.ShouldBe(0);
        _submission.IsSaving.ShouldBeFalse();
        _submission.Error.ShouldBeNull();
        _submission.Summary.ShouldBe([new ErrorSummaryItem("form-Email", "Enter a valid email.")]);
    }

    [Fact]
    public async Task A_message_on_an_unknown_field_blocks_the_save_without_a_summary_entry_and_shows_the_general_error()
    {
        // Arrange
        var store = new ValidationMessageStore(_context);
        store.Add(_context.Field("Other"), "Hidden.");

        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeFalse();
        _submission.Summary.Count.ShouldBe(0);
        _submission.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task Fixing_the_message_lets_the_next_submit_save()
    {
        // Arrange
        var store = new ValidationMessageStore(_context);
        store.Add(_context.Field("Email"), "Enter a valid email.");
        await _submission.Submit(() => Task.FromResult(Result.Ok()));
        store.Clear();

        // Act
        var saved = await _submission.Submit(() => Task.FromResult(Result.Ok()));

        // Assert
        saved.ShouldBeTrue();
        _submission.Summary.Count.ShouldBe(0);
    }

    /// <inheritdoc />
    public void Dispose() => _submission.Dispose();
}
