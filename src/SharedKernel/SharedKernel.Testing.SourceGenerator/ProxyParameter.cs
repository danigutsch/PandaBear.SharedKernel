namespace SharedKernel.Testing.SourceGenerator;

/// <summary>One parameter of a proxied method and the call property that shows its argument to hooks.</summary>
/// <param name="Name">The parameter name as an escaped C# identifier.</param>
/// <param name="RawName">The parameter name as declared.</param>
/// <param name="PropertyName">The name of the call property.</param>
/// <param name="Type">The fully qualified parameter type.</param>
/// <param name="Modifiers">The attributes and <c>params</c> modifier to repeat, followed by a space, or empty.</param>
internal sealed record ProxyParameter(string Name, string RawName, string PropertyName, string Type, string Modifiers);
