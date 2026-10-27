using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Finds the single container mount of a resource that matches a kind or target.</summary>
internal static class MountAnnotations
{
    /// <summary>Returns the only named-volume mount of the resource.</summary>
    /// <param name="resource">The resource to inspect.</param>
    public static ContainerMountAnnotation Volume(IResource resource) =>
        resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem(mount => mount.Type == ContainerMountType.Volume);

    /// <summary>Returns the only mount of the resource at the target folder.</summary>
    /// <param name="resource">The resource to inspect.</param>
    /// <param name="target">The container folder the mount targets.</param>
    public static ContainerMountAnnotation Mount(IResource resource, string target) =>
        resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem(mount => mount.Target == target);
}
