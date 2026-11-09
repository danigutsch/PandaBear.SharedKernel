using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the factory of a submission for a form that edits one kind of record.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ServerFormSubmissionForRecordTests
{
    [Theory]
    [InlineData(ResultStatus.Conflict, "The customer was changed or removed by someone else. Reload the page and try again.")]
    [InlineData(ResultStatus.NotFound, "That customer no longer exists.")]
    [InlineData(ResultStatus.Error, "We could not save the customer. Try again later.")]
    public async Task The_default_messages_use_the_noun(ResultStatus status, string expected)
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "customer", "customer");

        // Act
        await submission.Submit(() => Task.FromResult(FailedResults.For(status)));

        // Assert
        submission.Error.ShouldBe(expected);
    }

    [Fact]
    public async Task Field_errors_link_to_the_prefixed_control_ids_with_the_default_invalid_message()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(new EditContext(new ServerValidationModel()), ["Name"], "tour", "tour");
        var invalid = Result.Invalid("bad", "Name", "first line\nsecond line");

        // Act
        await submission.Submit(() => Task.FromResult(invalid));

        // Assert
        submission.Summary.Count.ShouldBe(1);
        submission.Summary[0].TargetId.ShouldBe("tour-Name");
        submission.Summary[0].Message.ShouldBe("This value is not valid.");
    }

    [Fact]
    public async Task Every_message_can_be_replaced()
    {
        // Arrange
        using var submission = ServerFormSubmission.ForRecord(
            new EditContext(new ServerValidationModel()), ["Name"], "c", "customer",
            invalidMessage: "Bad.", failureMessage: "Failed.", conflictMessage: "Changed.", notFoundMessage: "Gone.");

        // Act
        await submission.Submit(() => Task.FromResult(Result.Conflict("x")));
        var conflict = submission.Error;
        await submission.Submit(() => Task.FromResult(Result.NotFound("x")));
        var notFound = submission.Error;
        await submission.Submit(() => Task.FromResult(Result.Error("x")));
        var failure = submission.Error;
        await submission.Submit(() => Task.FromResult(Result.Invalid("x", "Name", "first line\nsecond line")));

        // Assert
        conflict.ShouldBe("Changed.");
        notFound.ShouldBe("Gone.");
        failure.ShouldBe("Failed.");
        submission.Summary[0].Message.ShouldBe("Bad.");
    }

    [Theory]
    [InlineData("", "customer")]
    [InlineData("customer", " ")]
    public void A_blank_prefix_or_noun_is_rejected(string prefix, string noun)
    {
        // Arrange
        var context = new EditContext(new ServerValidationModel());

        // Act
        Action creating = () => ServerFormSubmission.ForRecord(context, ["Name"], prefix, noun);

        // Assert
        creating.ShouldThrow<ArgumentException>();
    }
}
