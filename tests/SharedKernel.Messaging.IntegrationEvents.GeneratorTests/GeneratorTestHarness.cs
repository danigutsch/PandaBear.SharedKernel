using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Domain;
using PandaBear.Mediator;
using PandaBear.Messaging.IntegrationEvents.SourceGenerator;
using SharedHarness = PandaBear.Testing.Roslyn.GeneratorTestHarness;

namespace PandaBear.Messaging.IntegrationEvents.GeneratorTests;

/// <summary>Compiles integration event test sources and runs the integration event generators against them.</summary>
internal static class GeneratorTestHarness
{
    /// <summary>Hint name of the source file that the integration event generator emits.</summary>
    public const string GeneratedHintName = "SharedKernel.Messaging.IntegrationEvents.GeneratedIntegrationEvents.g.cs";

    private const string AssemblyName = "SharedKernel.Messaging.IntegrationEvents.Tests.Dynamic";

    private const string DefaultUsings = """
        using System;
        using PandaBear.Domain;
        using PandaBear.Messaging.IntegrationEvents;

        """;

    /// <summary>
    /// Compiles one source text, prefixed with the default usings, as a Preview-language C# compilation.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return SharedHarness.CreateCompilation([syntaxTree], AssemblyName, GetMetadataReferences());
    }

    /// <summary>
    /// Compiles several source texts, each prefixed with the default usings and named Source0000.cs, Source0001.cs, and so on.
    /// </summary>
    /// <param name="sources">The C# source texts in compilation order.</param>
    public static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        var syntaxTrees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(
            DefaultUsings + source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: $"Source{index:D4}.cs"));

        return SharedHarness.CreateCompilation(syntaxTrees, AssemblyName, GetMetadataReferences());
    }

    /// <summary>
    /// Runs the generators on the compilation and returns only the driver run result.
    /// </summary>
    /// <param name="compilation">The compilation to generate from.</param>
    /// <param name="generators">The generators to run, or none to run IntegrationEventMappingGenerator.</param>
    public static GeneratorDriverRunResult RunGeneratorDriver(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators) => RunGenerator(compilation, generators).RunResult;

    /// <summary>
    /// Runs the generators on the compilation and returns the run result together with the output compilation that includes generated sources.
    /// </summary>
    /// <param name="compilation">The compilation to generate from.</param>
    /// <param name="generators">The generators to run, or none to run IntegrationEventMappingGenerator.</param>
    public static (GeneratorDriverRunResult RunResult, CSharpCompilation OutputCompilation) RunGenerator(
        CSharpCompilation compilation,
        params IIncrementalGenerator[] generators)
    {
        IIncrementalGenerator[] selected = generators.Length == 0 ? [new IntegrationEventMappingGenerator()] : generators;
        var runResult = SharedHarness.RunAndUpdate(selected, compilation, out var outputCompilation, out _);
        return (runResult, outputCompilation);
    }

    /// <summary>
    /// Returns the text of the integration event generator's emitted source from the run result.
    /// </summary>
    /// <param name="runResult">The generator driver run result.</param>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult) =>
        SharedHarness.GetGeneratedSource(runResult, GeneratedHintName);

    private static MetadataReference[] GetMetadataReferences() =>
    [
        MetadataReference.CreateFromFile(typeof(IDomainEvent).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IIntegrationEvent).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Google.Protobuf.IMessage).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Protobuf.TestContracts.TourCreated).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Protobuf.ProtobufGeneratedConsumer).Assembly.Location),
    ];
}
