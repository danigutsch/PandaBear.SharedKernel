using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests that a content revision grows by one in the same SQLite transaction as every content change.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "ContentRevision")]
public sealed class RevisionTests
{
    [Fact]
    public async Task Adding_content_bumps_the_revision_by_one_and_returns_the_written_count()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();
        context.Notes.Add(new Note { Text = "a" });

        // Act
        var written = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(1);
        var revision = await database.Revision();
        revision.ShouldBe(1);
        context.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task One_save_that_adds_modifies_and_deletes_bumps_the_revision_once()
    {
        // Arrange
        using var database = new NoteDatabase();
        var kept = await database.Store("kept");
        var removed = await database.Store("removed");
        await using var context = database.CreateContext();
        (await context.Notes.SingleAsync(note => note.Id == kept, TestContext.Current.CancellationToken)).Text = "changed";
        context.Notes.Remove(await context.Notes.SingleAsync(note => note.Id == removed, TestContext.Current.CancellationToken));
        context.Notes.Add(new Note { Text = "added" });

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var revision = await database.Revision();
        revision.ShouldBe(3);
        var texts = await database.Texts();
        texts.ShouldBe(["added", "changed"]);
    }

    [Fact]
    public async Task A_save_without_changes_leaves_the_revision_alone()
    {
        // Arrange
        using var database = new NoteDatabase();
        await database.Store("a");
        await using var context = database.CreateContext();
        _ = await context.Notes.ToListAsync(TestContext.Current.CancellationToken);

        // Act
        var written = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(0);
        var revision = await database.Revision();
        revision.ShouldBe(1);
    }

    [Fact]
    public async Task A_change_made_in_place_is_detected_and_bumps_the_revision()
    {
        // Arrange
        using var database = new NoteDatabase();
        await database.Store("a");
        await using var context = database.CreateContext();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        (await context.Notes.SingleAsync(TestContext.Current.CancellationToken)).Text = "b";

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var revision = await database.Revision();
        revision.ShouldBe(2);
        var texts = await database.Texts();
        texts.ShouldBe(["b"]);
    }

    [Fact]
    public async Task A_change_to_the_revision_row_alone_is_saved_without_a_bump()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();
        (await context.Revisions.SingleAsync(TestContext.Current.CancellationToken)).Value = 10;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var revision = await database.Revision();
        revision.ShouldBe(10);
    }

    [Fact]
    public async Task A_failed_content_save_writes_nothing_and_leaves_no_transaction_open()
    {
        // Arrange
        using var database = new NoteDatabase();
        await database.Store("taken");
        await using var context = database.CreateContext();
        context.Notes.Add(new Note { Text = "new" });
        context.Notes.Add(new Note { Text = "taken" });

        // Act
        var thrown = await RevisionOutcomes.FailedSave(context);

        // Assert
        thrown.ShouldBeOfType<DbUpdateException>();
        context.Database.CurrentTransaction.ShouldBeNull();
        var revision = await database.Revision();
        revision.ShouldBe(1);
        var texts = await database.Texts();
        texts.ShouldBe(["taken"]);
    }

    [Fact]
    public async Task A_failed_bump_rolls_the_content_back()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();
        context.Notes.Add(new Note { Text = "a" });
        context.Notes.Add(new Note { Text = "b" });
        database.DropRevisionTable();

        // Act
        var thrown = await RevisionOutcomes.FailedSave(context);

        // Assert
        thrown.ShouldBeOfType<Microsoft.Data.Sqlite.SqliteException>();
        context.Database.CurrentTransaction.ShouldBeNull();
        var texts = await database.Texts();
        texts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Inside_the_caller_transaction_the_bump_rolls_back_with_the_content()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        context.Notes.Add(new Note { Text = "rolled back" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var open = context.Database.CurrentTransaction;

        // Act
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        // Assert
        open.ShouldBeSameAs(transaction);
        var revision = await database.Revision();
        revision.ShouldBe(0);
        var texts = await database.Texts();
        texts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Inside_the_caller_transaction_the_bump_commits_with_the_content()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        context.Notes.Add(new Note { Text = "committed" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        var revision = await database.Revision();
        revision.ShouldBe(1);
        var texts = await database.Texts();
        texts.ShouldBe(["committed"]);
    }

    [Fact]
    public async Task A_writer_holding_a_stale_revision_row_does_not_lose_a_bump()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var stale = database.CreateContext();
        _ = await stale.Revisions.SingleAsync(TestContext.Current.CancellationToken);
        await database.Store("first");
        stale.Notes.Add(new Note { Text = "second" });

        // Act
        await stale.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var revision = await database.Revision();
        revision.ShouldBe(2);
    }

    [Fact]
    public async Task Incrementing_the_revision_alone_adds_one()
    {
        // Arrange
        using var database = new NoteDatabase();
        await using var context = database.CreateContext();

        // Act
        var updated = await context.IncrementRevision<NoteRevision>(revision => revision.Value, TestContext.Current.CancellationToken);

        // Assert
        updated.ShouldBe(1);
        var revision = await database.Revision();
        revision.ShouldBe(1);
    }

    [Fact]
    public async Task A_selector_that_is_not_a_property_of_the_row_is_rejected()
    {
        // Arrange
        var outside = new NoteRevision();
        Func<Task> constant = () => RevisionOutcomes.Unused().IncrementRevision<NoteRevision>(_ => 5L, TestContext.Current.CancellationToken);
        Func<Task> computed = () => RevisionOutcomes.Unused().IncrementRevision<NoteRevision>(revision => revision.Value + 1, TestContext.Current.CancellationToken);
        Func<Task> captured = () => RevisionOutcomes.Unused().IncrementRevision<NoteRevision>(_ => outside.Value, TestContext.Current.CancellationToken);
        Func<Task> saved = () => RevisionOutcomes.Unused().SaveChangesIncrementingRevision<NoteRevision>(_ => 5L, _ => Task.FromResult(0), TestContext.Current.CancellationToken);

        // Act & Assert
        await constant.ShouldThrow<ArgumentException>();
        await computed.ShouldThrow<ArgumentException>();
        await captured.ShouldThrow<ArgumentException>();
        await saved.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Arrange
        Func<Task> incrementNoContext = () => RevisionDbContextExtensions.IncrementRevision<NoteRevision>(null!, revision => revision.Value, TestContext.Current.CancellationToken);
        Func<Task> incrementNoSelector = () => RevisionOutcomes.Unused().IncrementRevision<NoteRevision>(null!, TestContext.Current.CancellationToken);
        Func<Task> saveNoContext = () => RevisionDbContextExtensions.SaveChangesIncrementingRevision<NoteRevision>(null!, revision => revision.Value, _ => Task.FromResult(0), TestContext.Current.CancellationToken);
        Func<Task> saveNoSelector = () => RevisionOutcomes.Unused().SaveChangesIncrementingRevision<NoteRevision>(null!, _ => Task.FromResult(0), TestContext.Current.CancellationToken);
        Func<Task> saveNoSave = () => RevisionOutcomes.Unused().SaveChangesIncrementingRevision<NoteRevision>(revision => revision.Value, null!, TestContext.Current.CancellationToken);

        // Act & Assert
        await incrementNoContext.ShouldThrow<ArgumentNullException>();
        await incrementNoSelector.ShouldThrow<ArgumentNullException>();
        await saveNoContext.ShouldThrow<ArgumentNullException>();
        await saveNoSelector.ShouldThrow<ArgumentNullException>();
        await saveNoSave.ShouldThrow<ArgumentNullException>();
    }
}
