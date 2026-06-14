using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Discovers mediator contracts and emits an initial readable discovery report.
/// </summary>
[Generator]
public sealed class SharedKernelMediatorGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var discoveryModel = context.CompilationProvider.Select(
            static (compilation, cancellationToken) => DiscoveryModelBuilder.Build(compilation, cancellationToken))
            .WithTrackingName("DiscoveryModel");
        var generatorOptions = context.AnalyzerConfigOptionsProvider.Select(
            static (optionsProvider, _) => MediatorGeneratorConfigOptions.Parse(optionsProvider))
            .WithTrackingName("GeneratorOptions");
        var generationInput = discoveryModel.Combine(generatorOptions)
            .WithTrackingName("GenerationInput");

        context.RegisterSourceOutput(
            discoveryModel,
            static (productionContext, input) =>
            {
                foreach (var diagnostic in input.Diagnostics)
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }
            });

        var generatedSources = generationInput.Select(
                static (input, _) =>
                {
                    var discoveryModel = input.Left;
                    var generatorOptions = input.Right;

                    return (
                        DiscoveryReport: DiscoveryReportEmitter.Emit(discoveryModel),
                        DependencyInjection: DependencyInjectionEmitter.Emit(discoveryModel),
                        AppMediator: AppMediatorEmitter.Emit(discoveryModel),
                        AppMediatorTelemetry: AppMediatorTelemetryEmitter.Emit(),
                        GeneratedDispatch: GeneratedDispatchEmitter.Emit(discoveryModel),
                        GeneratedPipelines: GeneratedPipelinesEmitter.Emit(discoveryModel),
                        CallGraph: generatorOptions.EmitCallGraphJson
                            ? CallGraphArtifactEmitter.Emit(discoveryModel)
                            : null);
                })
            .WithTrackingName("GeneratedSources");

        context.RegisterSourceOutput(
            generatedSources,
            static (productionContext, output) =>
            {
                productionContext.AddSource(
                    GeneratedHintNames.DiscoveryReport,
                    SourceText.From(output.DiscoveryReport, Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.DependencyInjection,
                    SourceText.From(output.DependencyInjection, Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.AppMediator,
                    SourceText.From(output.AppMediator, Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.AppMediatorTelemetry,
                    SourceText.From(output.AppMediatorTelemetry, Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.GeneratedDispatch,
                    SourceText.From(output.GeneratedDispatch, Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.GeneratedPipelines,
                    SourceText.From(output.GeneratedPipelines, Encoding.UTF8));

                if (output.CallGraph is not null)
                {
                    productionContext.AddSource(
                        GeneratedHintNames.CallGraph,
                        SourceText.From(output.CallGraph, Encoding.UTF8));
                }
            });
    }
}
