using System.Net;
using System.Runtime.InteropServices;
using Amazon.S3;
using PandaBear.IO;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests <see cref="S3ObjectStore"/> over a real <see cref="AmazonS3Client"/> pipeline against a loopback stub.</summary>
public sealed class S3ObjectStoreRealSdkTests
{
    [Theory]
    [InlineData(1024, false)]
    [InlineData(1024, true)]
    [InlineData(5 * 1024 * 1024, false)]
    [InlineData(5 * 1024 * 1024, true)]
    public async Task Put_sends_exactly_the_bytes_of_a_read_only_memory_stream(int size, bool offHeap)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = new byte[size];
        new Random(42).NextBytes(payload);
        using var manager = new OffHeapMemoryManager(payload);
        var memory = offHeap ? manager.Memory : payload.AsMemory();
        MemoryMarshal.TryGetArray<byte>(memory, out _).ShouldBe(!offHeap);
        using var stub = new LoopbackS3Stub();
        using var client = RealSdkHarness.CreateClient(stub);
        var store = RealSdkHarness.CreateStore(client);
        await using var content = new ReadOnlyMemoryStream(memory);

        // Act
        var result = await store.Put(new ObjectWriteRequest("a/b.bin", content, "application/octet-stream", size), ct);

        // Assert
        var request = stub.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe("PUT");
        request.Path.ShouldBe("/media/a/b.bin");
        RealSdkHarness.DecodedBody(request).ShouldBe(payload);
        result.Length.ShouldBe(size);
    }

    [Fact]
    public async Task Put_resends_the_full_body_when_the_first_attempt_fails_with_500()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = new byte[5 * 1024 * 1024];
        new Random(7).NextBytes(payload);
        using var stub = new LoopbackS3Stub(HttpStatusCode.InternalServerError);
        using var client = RealSdkHarness.CreateClient(stub);
        var store = RealSdkHarness.CreateStore(client);
        await using var content = new ReadOnlyMemoryStream(payload);

        // Act
        await store.Put(new ObjectWriteRequest("a/b.bin", content, "application/octet-stream", payload.Length), ct);

        // Assert
        var requests = stub.Requests;
        requests.Count.ShouldBe(2);
        RealSdkHarness.DecodedBody(requests[0]).ShouldBe(payload);
        RealSdkHarness.DecodedBody(requests[1]).ShouldBe(payload);
    }
}
