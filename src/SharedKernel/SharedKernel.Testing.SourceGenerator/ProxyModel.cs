namespace PandaBear.Testing.SourceGenerator;

/// <summary>The proxy to generate for one service interface.</summary>
/// <param name="Key">The fully qualified interface without nullable annotations, which identifies it.</param>
/// <param name="ServiceType">The fully qualified interface.</param>
/// <param name="Display">The interface as shown in documentation comments.</param>
/// <param name="Methods">The proxied methods, in a stable order.</param>
internal sealed record ProxyModel(string Key, string ServiceType, string Display, EquatableArray<ProxyMethod> Methods);
