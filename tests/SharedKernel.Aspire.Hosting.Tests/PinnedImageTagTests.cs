using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests that the pinned PostgreSQL and Redis coordinates name the images the Aspire integrations use, and pin them by digest.</summary>
public sealed class PinnedImageTagTests
{
    [Fact]
    public void The_postgres_pin_names_the_aspire_default_image_and_applies_as_tag_and_digest()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var postgres = builder.AddPostgres("postgres");
        var aspireDefault = postgres.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var defaultRegistry = aspireDefault.Registry;
        var defaultImage = aspireDefault.Image;

        // Act
        postgres.WithImageTag(PostgresContainerImageTags.Tag).WithImageSHA256(PostgresContainerImageTags.Digest);
        var pinned = postgres.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();

        // Assert
        defaultRegistry.ShouldBe(PostgresContainerImageTags.Registry);
        defaultImage.ShouldBe(PostgresContainerImageTags.Image);
        pinned.SHA256.ShouldBe(PostgresContainerImageTags.Digest);
        pinned.Tag.ShouldBeNull("a digest replaces the tag in the image reference");
        PostgresContainerImageTags.Digest.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void The_redis_pin_names_the_aspire_default_image_and_applies_as_tag_and_digest()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var redis = builder.AddRedis("redis");
        var aspireDefault = redis.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var defaultRegistry = aspireDefault.Registry;
        var defaultImage = aspireDefault.Image;

        // Act
        redis.WithImageTag(RedisContainerImageTags.Tag).WithImageSHA256(RedisContainerImageTags.Digest);
        var pinned = redis.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();

        // Assert
        defaultRegistry.ShouldBe(RedisContainerImageTags.Registry);
        defaultImage.ShouldBe(RedisContainerImageTags.Image);
        pinned.SHA256.ShouldBe(RedisContainerImageTags.Digest);
        pinned.Tag.ShouldBeNull("a digest replaces the tag in the image reference");
        RedisContainerImageTags.Digest.ShouldMatch("^[0-9a-f]{64}$");
    }
}
