using System.Text;

namespace SharedKernel.IO.Tests;

/// <summary>Tests naming by content hash, write-if-absent publication, concurrent writers and name checks.</summary>
public sealed class ContentAddressedFileStoreTests : IDisposable
{
    private const string AbcName = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad.webp";

    private static readonly byte[] Abc = Encoding.ASCII.GetBytes("abc");

    private readonly TemporaryDirectory temporaryDirectory = new();

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public void Name_of_content_is_its_lowercase_hexadecimal_digest_and_the_extension()
    {
        // Act
        var name = ContentAddressedFileStore.NameOf(Abc, ".webp");

        // Assert
        name.ShouldBe(AbcName);
    }

    [Fact]
    public void Name_of_content_without_an_extension_is_its_hash_alone()
    {
        // Act
        var name = ContentAddressedFileStore.NameOf(Abc, string.Empty);

        // Assert
        name.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Theory]
    [InlineData("webp")]
    [InlineData(".")]
    [InlineData("..webp")]
    [InlineData(".we/bp")]
    [InlineData(".we\\bp")]
    [InlineData(".we bp")]
    [InlineData(".wébp")]
    public void Name_of_content_refuses_an_extension_that_is_not_a_dot_and_plain_letters_or_digits(string extension)
    {
        // Act
        Action name = () => ContentAddressedFileStore.NameOf(Abc, extension);

        // Assert
        name.ShouldThrow<ArgumentException>().ParamName.ShouldBe("extension");
    }

    [Fact]
    public async Task Store_writes_new_content_under_its_name_and_reports_it_created()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var stored = await store.Store(Abc, ".webp", ct);

        // Assert
        stored.ShouldBe(new StoredFile(AbcName, Created: true));
        var content = await File.ReadAllBytesAsync(Path.Combine(temporaryDirectory.Path, AbcName), ct);
        content.ShouldBe(Abc);
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([Path.Combine(temporaryDirectory.Path, AbcName)]);
    }

    [Fact]
    public async Task Store_of_content_already_present_keeps_the_file_and_reports_it_not_created()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(temporaryDirectory.Path, AbcName);
        await store.Store(Abc, ".webp", ct);
        var written = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, written.AddDays(-1));

        // Act
        var stored = await store.Store(Abc, ".webp", ct);

        // Assert
        stored.ShouldBe(new StoredFile(AbcName, Created: false));
        File.GetLastWriteTimeUtc(path).ShouldBe(written.AddDays(-1));
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task Store_creates_a_missing_directory()
    {
        // Arrange
        var directory = Path.Combine(temporaryDirectory.Path, "media", "files");
        var store = new ContentAddressedFileStore(directory);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var stored = await store.Store(Abc, ".webp", ct);

        // Assert
        var written = await File.ReadAllBytesAsync(Path.Combine(directory, AbcName), ct);
        stored.Created.ShouldBeTrue();
        written.ShouldBe(Abc);
    }

    [Fact]
    public async Task Concurrent_stores_of_the_same_content_create_one_complete_file_once()
    {
        // Arrange
        const int Writers = 16;
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);
        var ct = TestContext.Current.CancellationToken;
        var content = new byte[256 * 1024];
        new Random(550).NextBytes(content);
        var expectedName = ContentAddressedFileStore.NameOf(content, ".woff2");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var writes = Enumerable.Range(0, Writers)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task.WaitAsync(ct);
                return await store.Store(content, ".woff2", ct);
            }, ct))
            .ToArray();
        start.SetResult();
        var stored = await Task.WhenAll(writes);

        // Assert
        var path = Path.Combine(temporaryDirectory.Path, expectedName);
        var written = await File.ReadAllBytesAsync(path, ct);
        stored.ShouldAllSatisfy(file => file.Name.ShouldBe(expectedName));
        stored.Count(file => file.Created).ShouldBe(1);
        written.ShouldBe(content);
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task Store_that_cannot_publish_leaves_no_temporary_file()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);
        var occupied = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, AbcName));

        // Act
        var write = () => store.Store(Abc, ".webp", TestContext.Current.CancellationToken).AsTask();

        // Assert
        await write.ShouldThrow<IOException>();
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([occupied.FullName]);
    }

    [Fact]
    public async Task Canceled_store_creates_neither_the_directory_nor_the_file()
    {
        // Arrange
        var store = new ContentAddressedFileStore(Path.Combine(temporaryDirectory.Path, "media"));
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act
        var write = () => store.Store(Abc, ".webp", canceled.Token).AsTask();

        // Assert
        await write.ShouldThrow<OperationCanceledException>();
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task Store_refuses_an_invalid_extension_before_writing()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);

        // Act
        var write = () => store.Store(Abc, "/webp", TestContext.Current.CancellationToken).AsTask();

        // Assert
        await write.ShouldThrow<ArgumentException>();
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBeEmpty();
    }

    [Fact]
    public void Path_of_a_name_is_in_the_store_directory()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);

        // Act
        var path = store.PathOf("sample-cover_2.webp");

        // Assert
        path.ShouldBe(Path.Combine(Path.GetFullPath(temporaryDirectory.Path), "sample-cover_2.webp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".atomic-0123.tmp")]
    [InlineData("../outside.webp")]
    [InlineData("media/inside.webp")]
    [InlineData("media\\inside.webp")]
    [InlineData("c:inside.webp")]
    [InlineData("in side.webp")]
    public void Path_of_refuses_a_name_that_is_not_one_plain_file_name(string name)
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);

        // Act
        Action pathOf = () => store.PathOf(name);

        // Assert
        pathOf.ShouldThrow<ArgumentException>().ParamName.ShouldBe("name");
    }

    [Fact]
    public async Task Delete_removes_the_named_file_and_ignores_a_missing_one()
    {
        // Arrange
        var store = new ContentAddressedFileStore(temporaryDirectory.Path);
        var ct = TestContext.Current.CancellationToken;
        var stored = await store.Store(Abc, ".webp", ct);

        // Act
        store.Delete(stored.Name);
        store.Delete(stored.Name);

        // Assert
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBeEmpty();
    }

    [Fact]
    public void Delete_refuses_a_name_outside_the_directory()
    {
        // Arrange
        var store = new ContentAddressedFileStore(Path.Combine(temporaryDirectory.Path, "media"));
        var outside = Path.Combine(temporaryDirectory.Path, "outside.webp");
        File.WriteAllBytes(outside, Abc);

        // Act
        Action delete = () => store.Delete("../outside.webp");

        // Assert
        delete.ShouldThrow<ArgumentException>();
        File.Exists(outside).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_refuses_an_empty_directory(string directory)
    {
        // Act
        Action create = () => _ = new ContentAddressedFileStore(directory);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }
}
