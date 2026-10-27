namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds nullable-enabled source files that host assertion members for the user-message overload analyzer tests.</summary>
internal static class AssertionUserMessageOverloadTestSource
{
    /// <summary>Returns source with a public static SampleAssertions class containing the members, followed by any extra declarations.</summary>
    /// <param name="members">Member declarations placed inside SampleAssertions.</param>
    /// <param name="declarations">Additional top-level declarations appended after the class.</param>
    public static string Create(string members, string declarations = "") => $$"""
        #nullable enable
        using System.Diagnostics.CodeAnalysis;

        namespace Sample;

        public static class SampleAssertions
        {
            {{members}}
        }

        {{declarations}}
        """;
}
