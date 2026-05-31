using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers;

/// <summary>
/// Captures the analyzer-config switches that shape async-suffix rule behavior.
/// </summary>
internal readonly struct StyleAnalyzerConfigOptions(bool allowAsyncSuffixOverrides, bool allowAsyncSuffixInterfaceImplementations)
{
    private const string AllowAsyncSuffixOverridesKey = "sharedkernel_style_allow_async_suffix_overrides";
    private const string AllowAsyncSuffixInterfaceImplementationsKey = "sharedkernel_style_allow_async_suffix_interface_implementations";

    public bool AllowAsyncSuffixOverrides { get; } = allowAsyncSuffixOverrides;

    public bool AllowAsyncSuffixInterfaceImplementations { get; } = allowAsyncSuffixInterfaceImplementations;

    /// <summary>
    /// Reads the style-analyzer options from the current analyzer config provider.
    /// </summary>
    public static StyleAnalyzerConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        return new StyleAnalyzerConfigOptions(
            ReadBooleanOption(optionsProvider, AllowAsyncSuffixOverridesKey, defaultValue: true),
            ReadBooleanOption(optionsProvider, AllowAsyncSuffixInterfaceImplementationsKey, defaultValue: true));
    }

    private static bool ReadBooleanOption(AnalyzerConfigOptionsProvider optionsProvider, string key, bool defaultValue)
    {
        if (!optionsProvider.GlobalOptions.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }
}
