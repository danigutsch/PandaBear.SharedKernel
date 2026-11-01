namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Protects exact object identity and safe relative storage syntax.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
public sealed class ObjectStorageKeyTests
{
    [Fact]
    public void Upload_requests_preserve_the_existing_json_constructor_and_wire_shape()
    {
        // Arrange
        var request = ObjectUploadRequest.Create(ObjectStorageKey.From("media/Case.jpg"), "image/jpeg", 10, TimeSpan.FromMinutes(5));

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(request, ObjectStorageRequestJsonContext.Default.ObjectUploadRequest);
        var restored = System.Text.Json.JsonSerializer.Deserialize(json, ObjectStorageRequestJsonContext.Default.ObjectUploadRequest);

        // Assert
        restored.ShouldBe(request);
        restored.ShouldNotBeNull().ObjectKey.ShouldBe("media/Case.jpg");
        json.ShouldContain("\"ObjectKey\":\"media/Case.jpg\"", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("media/image.jpg")]
    [InlineData("media/Case sensitive #1.jpg")]
    public void Keys_preserve_exact_text_and_have_value_equality(string value)
    {
        // Arrange
        var first = ObjectStorageKey.From(value);

        // Act
        var second = ObjectStorageKey.From(value);

        // Assert
        second.ShouldBe(first);
        second.Value.ShouldBe(value);
        new HashSet<ObjectStorageKey> { first, second }.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/media/file")]
    [InlineData("C:/media/file")]
    [InlineData("media\\file")]
    [InlineData("media//file")]
    [InlineData("media/./file")]
    [InlineData("media/../file")]
    [InlineData("media/file\0.jpg")]
    [InlineData("media/file\n.jpg")]
    public void Unsafe_keys_are_rejected(string? value)
    {
        // Act
        var parsed = ObjectStorageKey.TryFrom(value, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void Case_is_significant_and_length_limits_do_not_change_identity()
    {
        // Arrange
        var key = ObjectStorageKey.From("Media/file", 10);

        // Act
        var lower = ObjectStorageKey.From("media/file", 100);
        var overlong = ObjectStorageKey.TryFrom("Media/file", out _, 9);

        // Assert
        key.ShouldNotBe(lower);
        overlong.ShouldBeFalse();
    }

    [Fact]
    public void Uninitialized_keys_cannot_reach_a_provider()
    {
        // Arrange
        var key = default(ObjectStorageKey);

        // Act
        Func<string> read = () => key.Value;

        // Assert
        read.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("media")]
    [InlineData("media/")]
    [InlineData("media///")]
    public void Prefixes_preserve_provider_matching_semantics(string value)
    {
        // Act
        var prefix = ObjectStoragePrefix.From(value);

        // Assert
        prefix.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("../")]
    [InlineData("media//child/")]
    [InlineData(" ")]
    public void Unsafe_prefixes_are_rejected(string value)
    {
        // Act
        var parsed = ObjectStoragePrefix.TryFrom(value, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void Escaping_preserves_segments_and_literal_percent_characters()
    {
        // Arrange
        var key = ObjectStorageKey.From("media/a b#%2F.jpg");

        // Act
        var escaped = key.ToEscapedPath();

        // Assert
        escaped.ShouldBe("media/a%20b%23%252F.jpg");
    }

    [Fact]
    public async Task Typed_adapters_preserve_the_provider_identity_and_listing_prefix()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        IObjectStore store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);
        var key = ObjectStorageKey.From("media/Case sensitive.jpg");
        var prefix = ObjectStoragePrefix.From("media/");

        // Act
        var exists = await store.Exists(key, TestContext.Current.CancellationToken);
        var keys = await store.ListKeys(prefix, TestContext.Current.CancellationToken);
        var uri = store.GetPublicUri(key);

        // Assert
        exists.ShouldBeTrue();
        keys.ShouldBe(["media/one.jpg", "media/two.jpg"]);
        client.Operations[0].ShouldBe("GetObjectMetadata:media/media/Case sensitive.jpg");
        client.Operations[1].ShouldBe("ListObjectsV2:media/media//");
        uri.OriginalString.ShouldBe("/cdn/media/Case%20sensitive.jpg");
    }
}
