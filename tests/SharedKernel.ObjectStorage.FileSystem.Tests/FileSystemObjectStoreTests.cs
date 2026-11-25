using System.Security.Cryptography;

namespace SharedKernel.ObjectStorage.FileSystem.Tests;

/// <summary>Exercises the file store through its provider-neutral contract.</summary>
public sealed class FileSystemObjectStoreTests
{
    [Fact]
    public async Task Stores_a_verified_venue_poster_with_application_owned_addresses_and_metadata()
    {
        // Arrange
        using var folder = new TemporaryDirectory();
        var store = VenueObjectStore.Create(folder.Path);
        var bytes = new byte[] { 1, 2, 3 };
        var checksum = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var content = new MemoryStream(bytes) { Position = 2 };
        const string key = "posters/open house.webp";
        var ct = TestContext.Current.CancellationToken;

        // Act
        var write = await store.Put(new ObjectWriteRequest(key, content, "image/webp", bytes.Length, checksum), ct);
        var inventory = await store.ListObjects("posters/", ct);
        using var read = await store.OpenRead(key, ct);
        await store.Delete(key, ct);
        using var copied = new MemoryStream();
        await read.Content.CopyToAsync(copied, ct);
        var exists = await store.Exists(key, ct);

        // Assert
        write.PublicUri.OriginalString.ShouldBe("/venue/posters/open%20house.webp");
        write.Checksum.ShouldBe(checksum);
        inventory.ShouldHaveSingleItem().ObjectKey.ShouldBe(key);
        read.ContentType.ShouldBe("image/webp");
        copied.ToArray().ShouldBe(bytes);
        content.CanRead.ShouldBeTrue();
        exists.ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(4, null)]
    [InlineData(3, "sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Rejects_length_or_checksum_mismatch_without_replacing_an_object(int length, string? checksum)
    {
        // Arrange
        using var folder = new TemporaryDirectory();
        var store = VenueObjectStore.Create(folder.Path);
        const string key = "posters/schedule.webp";
        using var initial = new MemoryStream([7, 8, 9]);
        using var replacement = new MemoryStream([1, 2, 3]);
        var ct = TestContext.Current.CancellationToken;
        await store.Put(new ObjectWriteRequest(key, initial, "image/webp", 3), ct);

        // Act
        Func<Task> put = async () => await store.Put(new ObjectWriteRequest(key, replacement, "image/webp", length, checksum), ct);

        // Assert
        await put.ShouldThrow<InvalidDataException>();
        Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories).ShouldHaveSingleItem()
            .ShouldBe(Path.Combine(folder.Path, "posters", "schedule.webp"));
        var persisted = await File.ReadAllBytesAsync(Path.Combine(folder.Path, "posters", "schedule.webp"), ct);
        persisted.ShouldBe([7, 8, 9]);
    }

    [Fact]
    public async Task Inventory_finds_unreferenced_venue_posters_and_missing_references_without_deleting()
    {
        // Arrange
        using var folder = new TemporaryDirectory();
        var store = VenueObjectStore.Create(folder.Path);
        Directory.CreateDirectory(Path.Combine(folder.Path, "posters"));
        const string oldKey = "posters/closed.webp";
        var date = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var oldPath = Path.Combine(folder.Path, "posters", "closed.webp");
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(oldPath, [7], ct);
        File.SetLastWriteTimeUtc(oldPath, date);
        await File.WriteAllBytesAsync(Path.Combine(folder.Path, "posters", "open.webp"), [8], ct);
        await File.WriteAllBytesAsync(Path.Combine(folder.Path, "posters", ".objects-00000000000000000000000000000000.tmp"), [9], ct);

        // Act
        var inventory = await store.ListObjects("posters/", ct);
        var report = ObjectInventoryReport.Compare(inventory, ["posters/open.webp", "posters/upcoming.webp", "posters/upcoming.webp", " "]);

        // Assert
        report.MissingObjectKeys.ShouldBe(["posters/upcoming.webp"]);
        var orphan = report.OrphanObjects.ShouldHaveSingleItem();
        orphan.ObjectKey.ShouldBe(oldKey);
        orphan.LastModifiedAt.ShouldBe(new DateTimeOffset(date));
        File.Exists(oldPath).ShouldBeTrue();
    }
}
