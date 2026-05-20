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
            static (compilation, cancellationToken) => DiscoveryModelBuilder.Build(compilation, cancellationToken));
        var generatorOptions = context.AnalyzerConfigOptionsProvider.Select(
            static (optionsProvider, _) => MediatorGeneratorConfigOptions.Parse(optionsProvider));
        var generationInput = discoveryModel.Combine(generatorOptions);

        context.RegisterSourceOutput(
            generationInput,
            static (productionContext, input) =>
            {
                var (discoveryModel, generatorOptions) = input;

                foreach (var diagnostic in discoveryModel.Diagnostics)
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }

                productionContext.AddSource(
                    GeneratedHintNames.DiscoveryReport,
                    SourceText.From(DiscoveryReportEmitter.Emit(discoveryModel), Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.DependencyInjection,
                    SourceText.From(DependencyInjectionEmitter.Emit(discoveryModel), Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.AppMediator,
                    SourceText.From(AppMediatorEmitter.Emit(discoveryModel), Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.AppMediatorTelemetry,
                    SourceText.From(AppMediatorTelemetryEmitter.Emit(), Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.GeneratedDispatch,
                    SourceText.From(GeneratedDispatchEmitter.Emit(discoveryModel), Encoding.UTF8));

                productionContext.AddSource(
                    GeneratedHintNames.GeneratedPipelines,
                    SourceText.From(GeneratedPipelinesEmitter.Emit(discoveryModel), Encoding.UTF8));

                if (generatorOptions.EmitCallGraphJson)
                {
                    productionContext.AddSource(
                        GeneratedHintNames.CallGraph,
                        SourceText.From(CallGraphArtifactEmitter.Emit(discoveryModel), Encoding.UTF8));
                }
            });
    }
}
