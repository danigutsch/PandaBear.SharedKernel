using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// Reads repository testing analyzer options from <c>.editorconfig</c>.
/// </summary>
internal sealed class TestingAnalyzerConfigOptions(ImmutableArray<RequiredTrait> requiredTraits)
{
    private const string RequiredTraitsKey = "sharedkernel_testing_required_traits";

    public ImmutableArray<RequiredTrait> RequiredTraits { get; } = requiredTraits;

    public static TestingAnalyzerConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? syntaxTree)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var value = TryGetOption(optionsProvider, syntaxTree, RequiredTraitsKey);
        if (value is not { } configuredTraits || string.IsNullOrWhiteSpace(configuredTraits))
        {
            return new TestingAnalyzerConfigOptions([]);
        }

        var traits = configuredTraits.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static entry => entry.Split(['='], 2, StringSplitOptions.None))
            .Where(static parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(static parts => new RequiredTrait(parts[0].Trim(), parts[1].Trim()))
            .ToImmutableArray();

        return new TestingAnalyzerConfigOptions(traits);
    }

    private static string? TryGetOption(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? syntaxTree, string key)
    {
        if (syntaxTree is not null
            && optionsProvider.GetOptions(syntaxTree).TryGetValue(key, out var syntaxTreeValue))
        {
            return syntaxTreeValue;
        }

        return optionsProvider.GlobalOptions.TryGetValue(key, out var value) ? value : null;
    }
}
