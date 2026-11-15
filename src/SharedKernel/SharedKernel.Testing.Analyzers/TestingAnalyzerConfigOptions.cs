using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers;

/// <summary>
/// Reads repository testing analyzer options from <c>.editorconfig</c>.
/// </summary>
internal sealed class TestingAnalyzerConfigOptions(
    ImmutableArray<RequiredTrait> requiredTraits,
    bool strictTestMethodCasing,
    ImmutableArray<string> allowedScopes,
    ImmutableArray<string> additionalAllowedSegments,
    ImmutableArray<string> invalidAdditionalAllowedSegments)
{
    private const string RequiredTraitsKey = "sharedkernel_testing_required_traits";
    private const string StrictTestMethodCasingKey = "sharedkernel_testing_strict_test_method_casing";
    private const string AllowedScopesKey = "sharedkernel_testing_allowed_scopes";
    private const string AdditionalAllowedSegmentsKey = "sharedkernel_testing_additional_allowed_segments";

    public ImmutableArray<RequiredTrait> RequiredTraits { get; } = requiredTraits;

    public bool StrictTestMethodCasing { get; } = strictTestMethodCasing;

    public ImmutableArray<string> AllowedScopes { get; } = allowedScopes;

    public ImmutableArray<string> AdditionalAllowedSegments { get; } = additionalAllowedSegments;

    public ImmutableArray<string> InvalidAdditionalAllowedSegments { get; } = invalidAdditionalAllowedSegments;

    public static TestingAnalyzerConfigOptions Parse(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? syntaxTree)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var strictTestMethodCasing = !string.Equals(
            TryGetOption(optionsProvider, syntaxTree, StrictTestMethodCasingKey)?.Trim(),
            "false",
            StringComparison.OrdinalIgnoreCase);
        var allowedScopes = ParseAllowedScopes(TryGetOption(optionsProvider, syntaxTree, AllowedScopesKey));
        var (additionalSegments, invalidSegments) = ParseAdditionalAllowedSegments(TryGetOption(optionsProvider, syntaxTree, AdditionalAllowedSegmentsKey));
        var value = TryGetOption(optionsProvider, syntaxTree, RequiredTraitsKey);
        if (value is not { } configuredTraits || string.IsNullOrWhiteSpace(configuredTraits))
        {
            return new TestingAnalyzerConfigOptions([], strictTestMethodCasing, allowedScopes, additionalSegments, invalidSegments);
        }

        var traits = configuredTraits.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static entry => entry.Split(['='], 2, StringSplitOptions.None))
            .Where(static parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(static parts => new RequiredTrait(parts[0].Trim(), parts[1].Trim()))
            .ToImmutableArray();

        return new TestingAnalyzerConfigOptions(traits, strictTestMethodCasing, allowedScopes, additionalSegments, invalidSegments);
    }

    private static ImmutableArray<string> ParseAllowedScopes(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return TestScopeValues.All;
        }

        var scopes = configured!.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static entry => entry.Trim())
            .Where(static entry => entry.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

        return scopes.IsDefaultOrEmpty ? TestScopeValues.All : scopes;
    }

    private static (ImmutableArray<string> Valid, ImmutableArray<string> Invalid) ParseAdditionalAllowedSegments(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return ([], []);
        }

        var valid = ImmutableArray.CreateBuilder<string>();
        var invalid = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in configured!.Split([',']).Select(static entry => entry.Trim()))
        {
            var target = entry.Length != 0 && char.IsLetter(entry[0]) && entry.All(char.IsLetterOrDigit) ? valid : invalid;
            if (!target.Contains(entry))
            {
                target.Add(entry);
            }
        }

        return (valid.ToImmutable(), invalid.ToImmutable());
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
