using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Builds the <see cref="DiscoveryModel"/> from a <see cref="Microsoft.CodeAnalysis.Compilation"/>.
/// Orchestrates assembly scanning, type collection, and descriptor construction.
/// Implementation is split across partial files by concern.
/// </summary>
internal static partial class DiscoveryModelBuilder
{
    private const string PrimaryAssemblyNamePropertyName = "PrimaryAssemblyName";
    private const int ParallelNotificationDispatchStrategyValue = 1;

    public static DiscoveryModel Build(Compilation compilation, CancellationToken cancellationToken)
    {
        var discoverySymbols = DiscoverySymbols.Create(compilation);
        var moduleAttribute = compilation.GetTypeByMetadataName(MetadataNames.MediatorModuleAttribute);

        if (!discoverySymbols.IsComplete)
        {
            return new DiscoveryModel(
                [],
                [],
                [],
                [],
                []);
        }

        var discoveryState = new DiscoveryState();
        var modules = new List<ModuleDescriptor>();

        ReportUnmarkedReferencedAssemblies(compilation, discoverySymbols, moduleAttribute, discoveryState, cancellationToken);

        foreach (var assembly in EnumerateAssemblies(compilation, moduleAttribute))
        {
            modules.Add(
                new ModuleDescriptor(
                    assembly.Identity.Name,
                    SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly),
                    HasModuleMarker(assembly, moduleAttribute)));

            CollectTypes(
                assembly.GlobalNamespace,
                discoverySymbols,
                discoveryState,
                compilation.Assembly,
                cancellationToken);
        }

        var requests = BuildRequestDescriptors(
            discoveryState.RequestContracts,
            discoveryState.RequestHandlers,
            discoveryState.Pipelines,
            compilation.Assembly,
            discoveryState);
        ReportRequestHandlerDiagnostics(requests, discoveryState);

        var streamRequests = BuildStreamRequestDescriptors(
            discoveryState.StreamRequestContracts,
            discoveryState.StreamHandlers,
            discoveryState.StreamPipelines,
            compilation.Assembly,
            discoveryState);
        ReportStreamRequestHandlerDiagnostics(discoveryState.StreamRequestContracts, discoveryState.StreamHandlers, discoveryState);

        return new DiscoveryModel(
            [.. modules.OrderBy(static module => module.AssemblyName, StringComparer.Ordinal)],
            requests,
            BuildNotificationDescriptors(discoveryState.NotificationContracts, discoveryState.NotificationHandlers, discoveryState),
            streamRequests,
            [.. discoveryState.Diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)]);
    }

    private static IEnumerable<IAssemblySymbol> EnumerateAssemblies(
        Compilation compilation,
        INamedTypeSymbol? moduleAttribute)
    {
        yield return compilation.Assembly;

        if (moduleAttribute is null)
        {
            yield break;
        }

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols.Where(
                     referencedAssembly => HasModuleMarker(referencedAssembly, moduleAttribute)))
        {
            yield return referencedAssembly;
        }
    }

    private static bool HasModuleMarker(IAssemblySymbol assembly, INamedTypeSymbol? moduleAttribute)
    {
        return moduleAttribute is not null
               && assembly.GetAttributes().Any(
                   attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, moduleAttribute));
    }

    private static void CollectTypes(
        INamespaceSymbol @namespace,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
        {
            CollectTypes(
                nestedNamespace,
                discoverySymbols,
                discoveryState,
                primaryAssembly,
                cancellationToken);
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            CollectType(
                type,
                discoverySymbols,
                discoveryState,
                primaryAssembly,
                cancellationToken);
        }
    }

    private static void CollectType(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryCreateRequestContract(type, discoverySymbols, primaryAssembly, out var requestContract))
        {
            discoveryState.RequestContracts[requestContract.MetadataName] = requestContract;
        }

        foreach (var requestHandler in CreateRequestHandlers(type, discoverySymbols, discoveryState, primaryAssembly))
        {
            discoveryState.RequestHandlers.Add(requestHandler);
        }

        foreach (var pipeline in CreatePipelines(type, discoverySymbols, discoveryState, primaryAssembly))
        {
            discoveryState.Pipelines.Add(pipeline);
        }

        if (TryCreateNotificationContract(type, discoverySymbols, out var notificationContract))
        {
            discoveryState.NotificationContracts[notificationContract.MetadataName] = notificationContract;
        }

        foreach (var notificationHandler in CreateNotificationHandlers(type, discoverySymbols, discoveryState, primaryAssembly))
        {
            discoveryState.NotificationHandlers.Add(notificationHandler);
        }

        if (TryCreateStreamRequestContract(type, discoverySymbols, primaryAssembly, out var streamRequestContract))
        {
            discoveryState.StreamRequestContracts[streamRequestContract.MetadataName] = streamRequestContract;
        }

        foreach (var streamHandler in CreateStreamHandlers(type, discoverySymbols, discoveryState, primaryAssembly))
        {
            discoveryState.StreamHandlers.Add(streamHandler);
        }

        foreach (var streamPipeline in CreateStreamPipelines(type, discoverySymbols, discoveryState, primaryAssembly))
        {
            discoveryState.StreamPipelines.Add(streamPipeline);
        }

        foreach (var nestedType in type.GetTypeMembers())
        {
            CollectType(
                nestedType,
                discoverySymbols,
                discoveryState,
                primaryAssembly,
                cancellationToken);
        }
    }

}
