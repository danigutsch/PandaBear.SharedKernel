namespace PandaBear.Testing.SourceGenerator;

/// <summary>The IDs of the diagnostics the CallProxy generator reports.</summary>
internal static class CallProxyDiagnosticIds
{
    /// <summary>The wrapped type is not an interface.</summary>
    public const string NotAnInterface = "SKPRX001";

    /// <summary>The wrapped interface cannot be implemented from the test assembly.</summary>
    public const string InaccessibleInterface = "SKPRX002";

    /// <summary>The wrapped interface has a member that gets no typed hooks.</summary>
    public const string UnsupportedMember = "SKPRX003";

    /// <summary>The project's C# version has no extension members.</summary>
    public const string LanguageVersion = "SKPRX004";
}
