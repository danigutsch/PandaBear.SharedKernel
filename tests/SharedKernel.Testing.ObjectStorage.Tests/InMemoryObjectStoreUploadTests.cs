using Microsoft.Extensions.Time.Testing;
using SharedKernel.ObjectStorage;

namespace SharedKernel.Testing.ObjectStorage.Tests;

/// <summary>Tests direct-upload tickets from the in-memory object store.</summary>
public sealed class InMemoryObjectStoreUploadTests
{
    [Fact]
    public static async Task CreateUploadUrl_returns_a_ticket_that_expires_after_the_requested_lifetime()
    {
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var store = new InMemoryObjectStore
        {
            TimeProvider = time,
            StorageBaseUri = new Uri("https://storage.example/bucket/")
        };

        // Act
        var ticket = await store.CreateUploadUrl(new ObjectUploadRequest("media/a b.jpg", "image/jpeg", 42, TimeSpan.FromMinutes(5)), TestContext.Current.CancellationToken);

        // Assert
        ticket.ObjectKey.ShouldBe("media/a b.jpg");
        ticket.UploadUri.ShouldBe(new Uri("https://storage.example/bucket/media/a%20b.jpg"));
        ticket.ExpiresAt.ShouldBe(new DateTimeOffset(2026, 1, 2, 3, 9, 5, TimeSpan.Zero));
        ticket.RequiredHeaders["content-type"].ShouldBe("image/jpeg");
        ticket.RequiredHeaders["Content-Length"].ShouldBe("42");
        ticket.RequiredHeaders.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(" ", 1, 60)]
    [InlineData("image/jpeg\r\nX-Injected: 1", 1, 60)]
    [InlineData("image/jpeg", 0, 60)]
    [InlineData("image/jpeg", 1, 0)]
    [InlineData("image/jpeg", 1, 604801)]
    public static async Task CreateUploadUrl_rejects_requests_the_s3_provider_rejects(string contentType, long length, int lifetimeSeconds)
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var request = new ObjectUploadRequest("media/a.jpg", contentType, length, TimeSpan.FromSeconds(lifetimeSeconds));

        // Act
        Func<Task> act = () => store.CreateUploadUrl(request, TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrowAssignableTo<ArgumentException>();
        store.Calls.ShouldBeEmpty();
    }
}
