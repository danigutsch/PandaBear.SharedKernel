namespace PandaBear.K6.Tool;

/// <summary>The container that <c>run --container</c> starts k6 in.</summary>
/// <param name="Engine"><c>docker</c> or <c>podman</c>.</param>
/// <param name="Image">The k6 image, pinned by digest, that <c>--image</c> can replace.</param>
internal sealed record K6ContainerConfiguration(string Engine, string Image);
