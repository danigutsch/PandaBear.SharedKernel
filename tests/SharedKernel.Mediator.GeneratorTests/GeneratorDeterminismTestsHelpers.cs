using System.Globalization;
using Microsoft.CodeAnalysis;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers that turn a generator run result into comparable values for determinism tests.</summary>
internal static class GeneratorDeterminismTestsHelpers
{
    /// <summary>Returns the single generator result's sources as a dictionary of hint name to source text, ordered by hint name.</summary>
    public static Dictionary<string, string> GetGeneratedSourceMap(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single()
            .GeneratedSources
            .OrderBy(static generated => generated.HintName, StringComparer.Ordinal)
            .ToDictionary(
                static generated => generated.HintName,
                static generated => generated.SourceText.ToString(),
                StringComparer.Ordinal);
    }

    /// <summary>Returns each diagnostic of the single generator result as an 'id|message' string using the invariant culture.</summary>
    public static string[] GetDiagnostics(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single()
            .Diagnostics
            .Select(static diagnostic =>
                $"{diagnostic.Id}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}")
            .ToArray();
    }
}
