using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.SourceGenerator;

/// <summary>The descriptors of the diagnostics the CallProxy generator reports.</summary>
internal static class CallProxyDiagnostics
{
    private const string Category = "SharedKernel.Testing.CallProxy";
    private const string HelpLinkBase = "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.SourceGenerator/README.md#";

    /// <summary>The wrapped type is not an interface.</summary>
    public static readonly DiagnosticDescriptor NotAnInterface = new(
        CallProxyDiagnosticIds.NotAnInterface,
        "CallProxy can only wrap an interface",
        "CallProxy can only wrap an interface; '{0}' is not an interface",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The CallProxy generator implements the wrapped type with a class that forwards each call, which needs an interface.",
        helpLinkUri: HelpLinkBase + "skprx001");

    /// <summary>The wrapped interface cannot be implemented from the test assembly.</summary>
    public static readonly DiagnosticDescriptor InaccessibleInterface = new(
        CallProxyDiagnosticIds.InaccessibleInterface,
        "CallProxy needs an interface the test assembly can implement",
        "CallProxy cannot implement '{0}', which is not accessible from this assembly",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated proxy is a top-level class of the test assembly, so the interface must be public or internal to it, not private or protected inside a type.",
        helpLinkUri: HelpLinkBase + "skprx002");

    /// <summary>The wrapped interface has a member that gets no typed hooks.</summary>
    public static readonly DiagnosticDescriptor UnsupportedMember = new(
        CallProxyDiagnosticIds.UnsupportedMember,
        "CallProxy supports interface methods without ref, out or in parameters only",
        "CallProxy cannot wrap '{0}': its member '{1}' is {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every member of the interface needs typed hooks: properties, indexers, events, generic methods, ref, out and in parameters, by-reference results, and ref struct or pointer types have none.",
        helpLinkUri: HelpLinkBase + "skprx003");

    /// <summary>The project's C# version has no extension members.</summary>
    public static readonly DiagnosticDescriptor LanguageVersion = new(
        CallProxyDiagnosticIds.LanguageVersion,
        "CallProxy typed hooks need C# 14",
        "CallProxy typed hooks are C# 14 extension members; this project compiles C# {0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The typed hooks such as hooks.Find are generated extension members of CallHooks<T>, which need C# 14, the default for .NET 10.",
        helpLinkUri: HelpLinkBase + "skprx004");

    /// <summary>Gets a descriptor by ID.</summary>
    /// <param name="id">The diagnostic ID.</param>
    /// <returns>The descriptor.</returns>
    public static DiagnosticDescriptor Get(string id) => id switch
    {
        CallProxyDiagnosticIds.NotAnInterface => NotAnInterface,
        CallProxyDiagnosticIds.InaccessibleInterface => InaccessibleInterface,
        CallProxyDiagnosticIds.UnsupportedMember => UnsupportedMember,
        _ => LanguageVersion,
    };
}
