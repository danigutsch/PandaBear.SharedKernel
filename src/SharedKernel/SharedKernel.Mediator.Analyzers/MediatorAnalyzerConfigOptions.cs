using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Mediator.Analyzers;

/// <summary>
/// Reads mediator analyzer options from <c>.editorconfig</c>.
/// </summary>
internal sealed class MediatorAnalyzerConfigOptions(bool cqrsStrict, bool allowHandlerToHandlerCalls, bool enableCancellationAnalysis)
{
    private const string CqrsStrictKey = "sharedkernel_mediator_cqrs_strict";
    private const string AllowHandlerCallsKey = "sharedkernel_mediator_allow_handler_to_handler_calls";
    private const string EnableCancellationAnalysisKey = "sharedkernel_mediator_enable_cancellation_analysis";

    public bool CqrsStrict { get; } = cqrsStrict;

    public bool AllowHandlerToHandlerCalls { get; } = allowHandlerToHandlerCalls;

    public bool EnableCancellationAnalysis { get; } = enableCancellationAnalysis;

    public static MediatorAnalyzerConfigOptions Default { get; } = new(
        cqrsStrict: true,
        allowHandlerToHandlerCalls: false,
        enableCancellationAnalysis: true);

    public static MediatorAnalyzerConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var globalOptions = optionsProvider.GlobalOptions;
        return new MediatorAnalyzerConfigOptions(
            cqrsStrict: TryGetBooleanOption(globalOptions, CqrsStrictKey, Default.CqrsStrict),
            allowHandlerToHandlerCalls: TryGetBooleanOption(globalOptions, AllowHandlerCallsKey, Default.AllowHandlerToHandlerCalls),
            enableCancellationAnalysis: TryGetBooleanOption(globalOptions, EnableCancellationAnalysisKey, Default.EnableCancellationAnalysis));
    }

    private static bool TryGetBooleanOption(AnalyzerConfigOptions options, string key, bool defaultValue)
    {
        return options.TryGetValue(key, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }
}
