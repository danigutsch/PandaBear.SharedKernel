namespace SharedKernel.Testing.SourceGenerator;

/// <summary>One proxied method and its typed hooks.</summary>
/// <param name="HookName">The name of the hooks member, as an escaped C# identifier.</param>
/// <param name="CallTypeName">The name of the generated call type.</param>
/// <param name="MethodName">The method name as an escaped C# identifier.</param>
/// <param name="DeclaringType">The fully qualified interface that declares the method.</param>
/// <param name="CastInner">Whether the real service must be cast to <paramref name="DeclaringType"/> to call the method.</param>
/// <param name="ReturnKind">How the method returns.</param>
/// <param name="ReturnType">The fully qualified return type.</param>
/// <param name="ResultType">The fully qualified result type, or <see langword="null"/> for a method without a result.</param>
/// <param name="ReturnAttributes">The return attributes to repeat, followed by a space, or empty.</param>
/// <param name="Display">The method as shown in documentation comments.</param>
/// <param name="Parameters">The parameters.</param>
internal sealed record ProxyMethod(
    string HookName,
    string CallTypeName,
    string MethodName,
    string DeclaringType,
    bool CastInner,
    ProxyReturnKind ReturnKind,
    string ReturnType,
    string? ResultType,
    string ReturnAttributes,
    string Display,
    EquatableArray<ProxyParameter> Parameters);
