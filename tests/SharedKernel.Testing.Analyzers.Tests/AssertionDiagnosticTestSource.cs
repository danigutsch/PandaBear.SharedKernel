namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds the sample test class source used by the assertion diagnostic tests.</summary>
internal static class AssertionDiagnosticTestSource
{
    /// <summary>Returns source for a SampleTests class whose Checks_behavior method declares sample locals (values, predicate, holder, text) and then contains the supplied statement, optionally without the [Fact] attribute.</summary>
    /// <param name="body">Statement text placed at the end of the test method body.</param>
    /// <param name="isTest">True to mark the method with [Fact], false to leave it a plain method.</param>
    public static string Create(string body, bool isTest = true) => $$"""
        using System.Linq;
        using PandaBear.Testing.Assertions;
        public sealed class SampleTests
        {
            {{(isTest ? "[Fact]" : "")}}
            public void Checks_behavior()
            {
                int[] values = [1];
                Func<int, bool> predicate = item => item > 0;
                Holder? holder = new Holder();
                string? text = "title";
                {{body}}
            }
        }
        public sealed class Holder
        {
            public string? Text { get; } = "title";
            public int Number { get; } = 1;
            public int Single() => 1;
            public void ShouldProceed() { }
        }
        """;
}
