using System.Reflection;

namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorDiscoveryReportTestsHelpers
{
    public static void AssertOrdered(string actual, params string[] expectedSegments)
    {
        var currentIndex = -1;

        foreach (var expectedSegment in expectedSegments)
        {
            var nextIndex = actual.IndexOf(expectedSegment, currentIndex + 1, StringComparison.Ordinal);
            Assert.True(nextIndex >= 0, $"Expected segment not found: {expectedSegment}");
            Assert.True(nextIndex > currentIndex, $"Expected segment out of order: {expectedSegment}");
            currentIndex = nextIndex;
        }
    }

    public static string LoadGeneratedCallGraphJson(string source, string generatedCallGraphSource)
    {
        const string runtimeUsings = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            """;
        var compilation = GeneratorTestHarness.CreateCompilation(
            [runtimeUsings + source, generatedCallGraphSource],
            assemblyName: "SharedKernel.Mediator.Tests.GeneratedCallGraphRuntime");
        var assembly = GeneratorTestHarness.LoadAssembly(compilation);
        var callGraphType = assembly.GetType("SharedKernel.Mediator.Generated.MediatorCallGraph", throwOnError: true)!;
        return (string)callGraphType
            .GetProperty("Json", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
    }
}
