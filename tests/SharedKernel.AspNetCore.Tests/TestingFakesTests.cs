using Microsoft.Extensions.Caching.Distributed;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests the shared form-file, browser-file and throwing-cache fakes.</summary>
public sealed class TestingFakesTests
{
    [Fact]
    public static async Task Fake_form_file_exposes_metadata_and_content()
    {
        // Arrange
        var file = new FakeFormFile([1, 2, 3], "a.bin", "image/png", "upload");

        // Act
        using var destination = new MemoryStream();
        await file.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Assert
        file.Length.ShouldBe(3);
        file.Name.ShouldBe("upload");
        file.FileName.ShouldBe("a.bin");
        file.ContentType.ShouldBe("image/png");
        destination.ToArray().ShouldBe([1, 2, 3]);
    }

    [Fact]
    public static void Fake_browser_file_rejects_reads_above_the_maximum_size()
    {
        // Arrange
        var file = new FakeBrowserFile([1, 2, 3], "a.bin");
        Action action = () => file.OpenReadStream(maxAllowedSize: 2);

        // Act, Assert
        action.ShouldThrow<IOException>();
    }

    [Fact]
    public static async Task Throwing_distributed_cache_throws_the_supplied_exception_from_every_operation()
    {
        // Arrange
        var failure = new InvalidOperationException("cache down");
        var cache = new ThrowingDistributedCache(failure);
        Action sync = () => cache.Get("k");
        Func<Task> async = () => cache.SetAsync("k", [1], new DistributedCacheEntryOptions(), TestContext.Current.CancellationToken);

        // Act, Assert
        sync.ShouldThrow<InvalidOperationException>().ShouldBeSameAs(failure);
        var thrown = await async.ShouldThrow<InvalidOperationException>();
        thrown.ShouldBeSameAs(failure);
    }
}
