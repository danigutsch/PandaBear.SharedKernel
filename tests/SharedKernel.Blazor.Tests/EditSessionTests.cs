using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the lifecycle of a page that creates or edits one record.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class EditSessionTests
{
    [Fact]
    public async Task A_new_record_is_ready_without_loading()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        var loads = 0;

        // Act
        await session.Sync(null, id => { loads++; return ValueTask.FromResult(Result.Ok(new EditSessionRecord("x", 1))); }, EditSessionFixtures.Fill);

        // Assert
        loads.ShouldBe(0);
        session.Context.ShouldNotBeNull();
        session.Submission.ShouldNotBeNull();
        session.Record.ShouldBeNull();
        session.IsMissing.ShouldBeFalse();
        session.Form.Name.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task An_existing_record_is_loaded_and_fills_the_form()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        var id = Guid.NewGuid();
        Guid? loaded = null;

        // Act
        await session.Sync(id, key => { loaded = key; return ValueTask.FromResult(Result.Ok(new EditSessionRecord("Bia", 3))); }, EditSessionFixtures.Fill);

        // Assert
        loaded.ShouldBe(id);
        session.Record.ShouldBe(new EditSessionRecord("Bia", 3));
        session.Form.Name.ShouldBe("Bia");
        session.Context.ShouldNotBeNull();
        session.Submission.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_record_restored_before_the_first_sync_is_not_loaded_again()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        session.Record = new EditSessionRecord("Prerendered", 2);
        var loads = 0;

        // Act
        await session.Sync(Guid.NewGuid(), _ => { loads++; return ValueTask.FromResult(Result.Ok(new EditSessionRecord("Loaded", 9))); }, EditSessionFixtures.Fill);

        // Assert
        loads.ShouldBe(0);
        session.Form.Name.ShouldBe("Prerendered");
        session.Record!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_record_that_cannot_be_loaded_is_missing_and_has_no_form()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();

        // Act
        await session.Sync(Guid.NewGuid(), _ => ValueTask.FromResult(Result.NotFound<EditSessionRecord>("gone")), EditSessionFixtures.Fill);

        // Assert
        session.IsMissing.ShouldBeTrue();
        session.Record.ShouldBeNull();
        session.Context.ShouldBeNull();
        session.Submission.ShouldBeNull();
    }

    [Fact]
    public async Task The_same_id_again_changes_nothing()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        var id = Guid.NewGuid();
        var loaded = new List<Guid>();
        await session.Sync(id, key => { loaded.Add(key); return ValueTask.FromResult(Result.Ok(new EditSessionRecord("Bia", 1))); }, EditSessionFixtures.Fill);
        var context = session.Context;
        var submission = session.Submission;
        session.Form.Name = "typed";

        // Act
        await session.Sync(id, key => { loaded.Add(key); return ValueTask.FromResult(Result.Ok(new EditSessionRecord("Bia", 1))); }, EditSessionFixtures.Fill);

        // Assert
        loaded.ShouldBe([id]);
        session.Context.ShouldBeSameAs(context);
        session.Submission.ShouldBeSameAs(submission);
        session.Form.Name.ShouldBe("typed");
    }

    [Fact]
    public async Task Another_id_starts_over_and_runs_the_reset()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await session.Sync(first, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("First", 1))), EditSessionFixtures.Fill);
        var oldSubmission = session.Submission;
        var resets = 0;

        // Act
        await session.Sync(second, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("Second", 5))), EditSessionFixtures.Fill, () => resets++);

        // Assert
        resets.ShouldBe(1);
        session.Record.ShouldBe(new EditSessionRecord("Second", 5));
        session.Form.Name.ShouldBe("Second");
        session.Submission.ShouldNotBeSameAs(oldSubmission);
    }

    [Fact]
    public async Task Going_from_an_existing_record_to_a_new_one_clears_the_record_and_the_form()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        await session.Sync(Guid.NewGuid(), _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("Bia", 1))), EditSessionFixtures.Fill);

        // Act
        await session.Sync(null, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("x", 1))), EditSessionFixtures.Fill);

        // Assert
        session.Record.ShouldBeNull();
        session.Form.Name.ShouldBe(string.Empty);
        session.IsMissing.ShouldBeFalse();
        session.Context.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_missing_record_is_forgotten_when_the_id_changes()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        await session.Sync(Guid.NewGuid(), _ => ValueTask.FromResult(Result.NotFound<EditSessionRecord>("gone")), EditSessionFixtures.Fill);

        // Act
        await session.Sync(null, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("x", 1))), EditSessionFixtures.Fill);

        // Assert
        session.IsMissing.ShouldBeFalse();
        session.Context.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_load_that_finishes_after_the_route_moved_is_dropped()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        var slow = new TaskCompletionSource<Result<EditSessionRecord>>();
        var first = session.Sync(Guid.NewGuid(), _ => new ValueTask<Result<EditSessionRecord>>(slow.Task), EditSessionFixtures.Fill);

        // Act
        await session.Sync(Guid.NewGuid(), _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("Newer", 2))), EditSessionFixtures.Fill);
        slow.SetResult(Result.Ok(new EditSessionRecord("Older", 1)));
        await first;

        // Assert
        session.Record.ShouldBe(new EditSessionRecord("Newer", 2));
        session.Form.Name.ShouldBe("Newer");
    }

    [Fact]
    public async Task The_submission_uses_the_prefix_and_the_noun()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();
        await session.Sync(null, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("x", 1))), EditSessionFixtures.Fill);

        // Act
        await session.Submission!.Submit(() => Task.FromResult(Result.Invalid("bad", "Name", "Required")));
        var summary = session.Submission.Summary;
        await session.Submission.Submit(() => Task.FromResult(Result.Conflict("stale")));

        // Assert
        summary.ShouldHaveSingleItem().TargetId.ShouldBe("item-Name");
        session.Submission.Error.ShouldBe("The item was changed or removed by someone else. Reload the page and try again.");
    }

    [Fact]
    public async Task Sync_rejects_missing_callbacks()
    {
        // Arrange
        using var session = EditSessionFixtures.Create();

        // Act
        Func<Task> noLoad = () => session.Sync(null, null!, EditSessionFixtures.Fill);
        Func<Task> noFill = () => session.Sync(null, _ => ValueTask.FromResult(Result.Ok(new EditSessionRecord("x", 1))), null!);

        // Assert
        await noLoad.ShouldThrow<ArgumentNullException>();
        await noFill.ShouldThrow<ArgumentNullException>();
    }
}
