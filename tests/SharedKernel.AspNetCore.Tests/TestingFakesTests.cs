using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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

    [Fact]
    public static void Fake_browser_file_honors_a_canceled_token()
    {
        // Arrange
        var file = new FakeBrowserFile([1], "a.bin");
        Action action = () => file.OpenReadStream(cancellationToken: new CancellationToken(canceled: true));

        // Act, Assert
        action.ShouldThrow<OperationCanceledException>();
    }

    [Fact]
    public static void Throwing_distributed_cache_preserves_the_original_stack_trace()
    {
        // Arrange
        Action thrower = () => throw new InvalidOperationException("cache down");
        var failure = thrower.ShouldThrow<InvalidOperationException>();
        var cache = new ThrowingDistributedCache(failure);
        Action action = () => cache.Remove("k");

        // Act
        var thrown = action.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.StackTrace.ShouldContain(nameof(Throwing_distributed_cache_preserves_the_original_stack_trace), StringComparison.Ordinal);
    }

    [Fact]
    public static void Fake_browser_file_records_the_requested_limit_and_reports_the_declared_size()
    {
        // Arrange
        var file = new FakeBrowserFile([1, 2, 3], "a.bin") { DeclaredSize = 10 };
        Action action = () => file.OpenReadStream(maxAllowedSize: 5);

        // Act
        action.ShouldThrow<IOException>();

        // Assert
        file.Size.ShouldBe(10);
        file.MaximumAllowedSize.ShouldBe(5);
    }

    [Fact]
    public static void Fake_browser_file_throws_the_open_failure_after_recording_the_limit()
    {
        // Arrange
        var failure = new InvalidOperationException("open failed");
        var file = new FakeBrowserFile([1], "a.bin") { OpenFailure = failure };
        Action action = () => file.OpenReadStream(maxAllowedSize: 7);

        // Act, Assert
        action.ShouldThrow<InvalidOperationException>().ShouldBeSameAs(failure);
        file.MaximumAllowedSize.ShouldBe(7);
    }

    [Fact]
    public static async Task Throwing_distributed_cache_throws_selectively_and_forwards_the_rest()
    {
        // Arrange
        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var failure = new InvalidOperationException("cache down");
        var cache = new ThrowingDistributedCache(memory, failure, static (operation, _, call) => operation == DistributedCacheOperation.Remove && call == 1);
        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(1) };
        using var cancellation = new CancellationTokenSource();

        // Act
        await cache.SetAsync("k", [1], options, TestContext.Current.CancellationToken);
        var firstRemove = () => cache.RemoveAsync("k", cancellation.Token);
        var thrown = await firstRemove.ShouldThrow<InvalidOperationException>();
        thrown.ShouldBeSameAs(failure);
        await cache.RemoveAsync("k", TestContext.Current.CancellationToken);

        // Assert
        cache.LastSetOptions.ShouldBeSameAs(options);
        cache.CallCount(DistributedCacheOperation.Remove).ShouldBe(2);
        cache.LastRemoveCancellationToken.ShouldBe(TestContext.Current.CancellationToken);
        var remaining = await cache.GetAsync("k", TestContext.Current.CancellationToken);
        remaining.ShouldBeNull();
    }
}
