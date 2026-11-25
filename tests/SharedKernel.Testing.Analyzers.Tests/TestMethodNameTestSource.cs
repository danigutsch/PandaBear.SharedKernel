using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Builds the sources and options the SKTEST002 segment tests analyze.</summary>
internal static class TestMethodNameTestSource
{
    /// <summary>The analyzer config key that lists a repository's additional segments.</summary>
    public const string AdditionalSegmentsKey = "sharedkernel_testing_additional_allowed_segments";

    /// <summary>Gets every built-in entry whose spelling is forced, with its lowercase form.</summary>
    public static TheoryData<string, string> ForcedBuiltInEntries =>
        [.. TestMethodNameSegments.BuiltIn
            .Where(static entry => entry != "Task")
            .Select(static entry => (entry, new string([.. entry.Select(char.ToLowerInvariant)])))];

    /// <summary>Gets every built-in entry.</summary>
    public static TheoryData<string> BuiltInEntries => [.. TestMethodNameSegments.BuiltIn];

    /// <summary>Returns analyzer options that configure the given additional segments.</summary>
    /// <param name="value">The raw option value.</param>
    public static ImmutableDictionary<string, string> Configured(string value)
    {
        return ImmutableDictionary<string, string>.Empty.Add(AdditionalSegmentsKey, value);
    }

    /// <summary>Returns a test class with one fact of the given name.</summary>
    /// <param name="methodName">Name of the fact.</param>
    public static string Create(string methodName)
    {
        return $$"""
            namespace Demo;

            public sealed class SampleTests
            {
                [Fact]
                public void {{methodName}}()
                {
                }
            }
            """;
    }
}
