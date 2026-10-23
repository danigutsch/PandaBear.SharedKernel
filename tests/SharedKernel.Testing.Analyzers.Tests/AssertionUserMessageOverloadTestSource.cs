namespace PandaBear.Testing.Analyzers.Tests;

internal static class AssertionUserMessageOverloadTestSource
{
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
