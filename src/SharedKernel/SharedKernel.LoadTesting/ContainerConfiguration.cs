namespace PandaBear.LoadTesting;

/// <summary>The container that a container run starts the load generator in.</summary>
/// <param name="Engine"><c>docker</c> or <c>podman</c>.</param>
/// <param name="Image">The image, pinned by digest, that <see cref="LoadTestSettings.Image"/> can replace.</param>
internal sealed record ContainerConfiguration(string Engine, string Image);
