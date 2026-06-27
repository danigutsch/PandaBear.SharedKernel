using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// Reads repository testing analyzer options from <c>.editorconfig</c>.
/// </summary>
internal sealed class TestingAnalyzerConfigOptions(ImmutableArray<RequiredTrait> requiredTraits, bool strictTestMethodCasing)
{
    private const string RequiredTraitsKey = "sharedkernel_testing_required_traits";
    private const string StrictTestMethodCasingKey = "sharedkernel_testing_strict_test_method_casing";

    public ImmutableArray<RequiredTrait> RequiredTraits { get; } = requiredTraits;

    public bool StrictTestMethodCasing { get; } = strictTestMethodCasing;

    public static TestingAnalyzerConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? syntaxTree)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var strictTestMethodCasing = !string.Equals(
            TryGetOption(optionsProvider, syntaxTree, StrictTestMethodCasingKey),
            "false",
            StringComparison.OrdinalIgnoreCase);
        var value = TryGetOption(optionsProvider, syntaxTree, RequiredTraitsKey);
        if (value is not { } configuredTraits || string.IsNullOrWhiteSpace(configuredTraits))
        {
            return new TestingAnalyzerConfigOptions([], strictTestMethodCasing);
        }

        var traits = configuredTraits.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static entry => entry.Split(['='], 2, StringSplitOptions.None))
            .Where(static parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(static parts => new RequiredTrait(parts[0].Trim(), parts[1].Trim()))
            .ToImmutableArray();

        return new TestingAnalyzerConfigOptions(traits, strictTestMethodCasing);
    }

    private static string? TryGetOption(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? syntaxTree, string key)
    {
        var syntaxTreeValue = syntaxTree is null ? null : TryGetOptionValue(optionsProvider.GetOptions(syntaxTree), key);

        return syntaxTreeValue ?? TryGetOptionValue(optionsProvider.GlobalOptions, key);
    }

    private static string? TryGetOptionValue(AnalyzerConfigOptions options, string key)
    {
        return options.TryGetValue(key, out var value) ? value : null;
    }
}
