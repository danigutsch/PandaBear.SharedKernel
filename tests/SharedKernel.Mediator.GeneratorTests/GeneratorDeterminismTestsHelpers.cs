using System.Globalization;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorDeterminismTestsHelpers
{
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

    public static string[] GetDiagnostics(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single()
            .Diagnostics
            .Select(static diagnostic =>
                $"{diagnostic.Id}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}")
            .ToArray();
    }
}
