namespace SharedKernel.AspNetCore.Analyzers;

/// <summary>Diagnostic metadata shared by the analyzer and the code fix.</summary>
internal static class RuntimeEndpointDiagnostics
{
    /// <summary>The diagnostic for missing annotations on a runtime-bound mapping.</summary>
    public const string Id = "SKASPNET001";

    /// <summary>Full name of the dynamic-code annotation.</summary>
    public const string DynamicCode = "System.Diagnostics.CodeAnalysis." + DynamicCodeName;

    /// <summary>Full name of the trimming annotation.</summary>
    public const string UnreferencedCode = "System.Diagnostics.CodeAnalysis." + UnreferencedCodeName;

    /// <summary>Simple name of the dynamic-code annotation.</summary>
    public const string DynamicCodeName = "RequiresDynamicCodeAttribute";

    /// <summary>Simple name of the trimming annotation.</summary>
    public const string UnreferencedCodeName = "RequiresUnreferencedCodeAttribute";
}
