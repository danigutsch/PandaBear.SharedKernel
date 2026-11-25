using SharedKernel.ObjectStorage;

namespace SharedKernel.Testing.ObjectStorage.Tests;

/// <summary>Tests failure injection on the in-memory object store.</summary>
public sealed class InMemoryObjectStoreFailureTests
{
    [Fact]
    public static async Task FailNext_throws_the_chosen_exception_once_and_then_the_operation_succeeds()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        var failure = new IOException("delete failed");
        store.FailNext(ObjectStoreOperation.Delete, failure);
        var ct = TestContext.Current.CancellationToken;

        // Act
        Func<Task> first = () => store.Delete("media/a.jpg", ct).AsTask();
        var thrown = await first.ShouldThrow<IOException>();
        await store.Delete("media/a.jpg", ct);

        // Assert
        thrown.ShouldBeSameAs(failure);
        store.CallCount(ObjectStoreOperation.Delete).ShouldBe(2);
        store.Deletes.ShouldBe(["media/a.jpg"]);
        store.ObjectKeys.ShouldBeEmpty();
    }

    [Fact]
    public static async Task A_failure_scoped_to_a_key_leaves_other_keys_working()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        store.Seed("media/b.jpg", [1], "image/jpeg");
        store.FailNext(ObjectStoreOperation.Delete, new IOException("delete failed"), "media/b.jpg");
        var ct = TestContext.Current.CancellationToken;

        // Act
        await store.Delete("media/a.jpg", ct);
        Func<Task> scoped = () => store.Delete("media/b.jpg", ct).AsTask();

        // Assert
        await scoped.ShouldThrow<IOException>();
        store.ObjectKeys.ShouldBe(["media/b.jpg"]);
    }

    [Fact]
    public static async Task FailAlways_keeps_failing_until_the_failures_are_cleared()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        store.FailAlways(ObjectStoreOperation.OpenRead, new UnauthorizedAccessException("denied"));
        var ct = TestContext.Current.CancellationToken;
        Func<Task> read = () => store.OpenRead("media/a.jpg", ct).AsTask();

        // Act
        await read.ShouldThrow<UnauthorizedAccessException>();
        await read.ShouldThrow<UnauthorizedAccessException>();
        store.ClearFailures();
        using var result = await store.OpenRead("media/a.jpg", ct);

        // Assert
        result.Length.ShouldBe(1);
        store.CallCount(ObjectStoreOperation.OpenRead).ShouldBe(3);
        store.Reads.ShouldHaveSingleItem();
    }

    [Fact]
    public static async Task A_failed_put_neither_stores_nor_reads_the_content()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.FailNext(ObjectStoreOperation.Put, new IOException("put failed"));
        using var content = new MemoryStream([1, 2]);

        // Act
        Func<Task> act = () => store.Put(new ObjectWriteRequest("media/a.jpg", content, "image/jpeg", 2), TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrow<IOException>();
        content.Position.ShouldBe(0);
        store.ObjectKeys.ShouldBeEmpty();
        store.Puts.ShouldBeEmpty();
        store.Calls.ShouldBe([new ObjectStoreCall(ObjectStoreOperation.Put, "media/a.jpg")]);
    }

    [Fact]
    public static void A_get_public_URI_failure_throws_synchronously()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.FailNext(ObjectStoreOperation.GetPublicUri, new InvalidOperationException("no cdn"));

        // Act
        Action act = () => store.GetPublicUri("media/a.jpg");

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("no cdn");
        store.GetPublicUri("media/a.jpg").ShouldNotBeNull();
    }

    [Fact]
    public static async Task The_first_registered_matching_failure_wins()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.FailNext(ObjectStoreOperation.Exists, new IOException("first"));
        store.FailNext(ObjectStoreOperation.Exists, new TimeoutException("second"));
        var ct = TestContext.Current.CancellationToken;
        Func<Task> exists = () => store.Exists("media/a.jpg", ct).AsTask();

        // Act
        var first = await exists.ShouldThrow<IOException>();
        var second = await exists.ShouldThrow<TimeoutException>();

        // Assert
        first.Message.ShouldBe("first");
        second.Message.ShouldBe("second");
        var existsAfterFailures = await store.Exists("media/a.jpg", ct);
        existsAfterFailures.ShouldBeFalse();
    }

    [Fact]
    public static void Failure_registration_rejects_a_null_exception_and_an_undefined_operation()
    {
        // Arrange
        var store = new InMemoryObjectStore();

        // Act
        Action nullException = () => store.FailNext(ObjectStoreOperation.Put, null!);
        Action undefined = () => store.FailAlways((ObjectStoreOperation)99, new IOException());

        // Assert
        nullException.ShouldThrow<ArgumentNullException>();
        undefined.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
