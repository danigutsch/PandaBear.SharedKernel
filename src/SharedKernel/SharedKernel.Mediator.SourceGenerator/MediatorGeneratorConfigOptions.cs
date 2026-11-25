using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Reads mediator source-generator options from compiler-visible properties and analyzer configuration.
/// </summary>
internal sealed record MediatorGeneratorConfigOptions(bool EmitCallGraphJson)
{
    private const string EmitCallGraphJsonKey = "sharedkernel_mediator_emit_call_graph_json";
    private const string EmitCallGraphJsonBuildPropertyKey = "build_property.SharedKernelMediatorEmitCallGraphJson";

    public static MediatorGeneratorConfigOptions Default { get; } = new(false);

    public static MediatorGeneratorConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var globalOptions = optionsProvider.GlobalOptions;
        var configuredValue = TryGetBooleanOption(globalOptions, EmitCallGraphJsonKey, Default.EmitCallGraphJson);
        return new MediatorGeneratorConfigOptions(
            TryGetBooleanOption(globalOptions, EmitCallGraphJsonBuildPropertyKey, configuredValue));
    }

    private static bool TryGetBooleanOption(AnalyzerConfigOptions options, string key, bool defaultValue)
    {
        return options.TryGetValue(key, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }
}
