namespace PandaBear.Testing.Analyzers.Tests;

internal static class AssertionDiagnosticTestSource
{
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
