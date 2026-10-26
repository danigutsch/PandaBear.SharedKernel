using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Domain;
using PandaBear.Mediator;
using PandaBear.Messaging.IntegrationEvents.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Messaging.IntegrationEvents.GeneratorTests;

internal static class GeneratorTestHarness
{
    public const string GeneratedHintName = "SharedKernel.Messaging.IntegrationEvents.GeneratedIntegrationEvents.g.cs";

    private const string AssemblyName = "SharedKernel.Messaging.IntegrationEvents.Tests.Dynamic";

    private const string DefaultUsings = """
        using System;
        using PandaBear.Domain;
        using PandaBear.Messaging.IntegrationEvents;

        """;

    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation([syntaxTree], AssemblyName, GetMetadataReferences());
    }

    public static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        var syntaxTrees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(
            DefaultUsings + source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: $"Source{index:D4}.cs"));

        return SharedHarness.CreateCompilation(syntaxTrees, AssemblyName, GetMetadataReferences());
    }

    public static GeneratorDriverRunResult RunGeneratorDriver(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators) => RunGenerator(compilation, generators).RunResult;

    public static (GeneratorDriverRunResult RunResult, CSharpCompilation OutputCompilation) RunGenerator(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators)
    {
        IIncrementalGenerator[] selected = generators.Length == 0 ? [new IntegrationEventMappingGenerator()] : generators;
        var runResult = SharedHarness.RunAndUpdate(selected, compilation, out var outputCompilation, out _);
        return (runResult, outputCompilation);
    }

    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);

    private static MetadataReference[] GetMetadataReferences() =>
    [
        MetadataReference.CreateFromFile(typeof(IDomainEvent).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IIntegrationEvent).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
    ];
}
