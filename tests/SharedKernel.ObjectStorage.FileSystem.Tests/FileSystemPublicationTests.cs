namespace SharedKernel.ObjectStorage.FileSystem.Tests;

/// <summary>Checks publication boundaries and directory containment for the filesystem adapter.</summary>
public sealed class FileSystemPublicationTests
{
    [Fact]
    public async Task Cancellation_after_a_chunk_cleans_up_the_unpublished_file()
    {
        // Arrange
        using var folder = new TemporaryDirectory();
        var store = new FileSystemObjectStore(new FileSystemObjectStoreOptions(
            folder.Path, key => new Uri("/" + key.ToEscapedPath(), UriKind.Relative), _ => ("image/webp", null)));
        using var content = new GatedChunkReadStream([1, 2, 3], chunkSize: 1);
        var ct = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var guard = TimeSpan.FromSeconds(10);
        var pending = store.Put(new ObjectWriteRequest("poster.webp", content, "image/webp", 3), cancellation.Token).AsTask();
        await content.WaitForPendingRead(ct).WaitAsync(guard, ct);
        content.ReleaseRead();
        await content.WaitForPendingRead(ct).WaitAsync(guard, ct);

        // Act
        var inventoryWhileWriting = await store.ListObjects("", ct);
        await cancellation.CancelAsync();

        // Assert
        Func<Task> result = () => pending.WaitAsync(guard, ct);
        await result.ShouldThrowAssignableTo<OperationCanceledException>();
        inventoryWhileWriting.ShouldBeEmpty();
        Directory.EnumerateFileSystemEntries(folder.Path).ShouldBeEmpty();
        content.BytesRead.ShouldBe(1);
        content.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_link_inside_the_root_is_not_an_inventory_entry_or_a_read_path()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var folder = new TemporaryDirectory();
        using var linkedFolder = new TemporaryDirectory();
        var store = new FileSystemObjectStore(new FileSystemObjectStoreOptions(
            folder.Path, key => new Uri("/" + key.ToEscapedPath(), UriKind.Relative), _ => ("image/webp", null)));
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(Path.Combine(linkedFolder.Path, "poster.webp"), [1], ct);
        Directory.CreateSymbolicLink(Path.Combine(folder.Path, "linked"), linkedFolder.Path);

        // Act
        var inventory = await store.ListObjects("", ct);
        Func<Task> read = async () => (await store.OpenRead("linked/poster.webp", ct)).Dispose();

        // Assert
        inventory.ShouldBeEmpty();
        await read.ShouldThrow<InvalidOperationException>();
    }
}
