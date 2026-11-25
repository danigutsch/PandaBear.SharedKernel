namespace SharedKernel.LoadTesting.Tests;

/// <summary>Tests the engine and image rules of a container run.</summary>
public sealed class LoadTestContainerTests
{
    private const string Pinned = "grafana/k6:1.0.0@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(Pinned, true)]
    [InlineData("grafana/k6:1.0.0", false)]
    [InlineData("-grafana/k6@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("grafana/k6@sha256:0123", false)]
    public void Only_references_with_a_sha256_digest_are_pinned(string image, bool expected)
    {
        // Act
        var pinned = LoadTestContainer.IsDigestPinned(image);

        // Assert
        pinned.ShouldBe(expected);
    }

    [Theory]
    [InlineData("lxc", Pinned, "engine")]
    [InlineData("docker", "grafana/k6:1.0.0", "image")]
    public void Rejects_an_unknown_engine_or_an_unpinned_image(string engine, string image, string parameter)
    {
        // Act
        var exception = Record.Exception(() => new LoadTestContainer(engine, image));

        // Assert
        exception.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe(parameter);
    }
}
