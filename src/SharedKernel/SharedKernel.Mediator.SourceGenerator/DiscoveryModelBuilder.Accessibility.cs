using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Accessibility checks, module candidate detection, and related diagnostic reporting
/// for types that span referenced assemblies.
/// </summary>
internal static partial class DiscoveryModelBuilder
{
    private static bool IsAccessibleToGeneratedMediator(INamedTypeSymbol type, IAssemblySymbol primaryAssembly)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    continue;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (HasInternalAccessToPrimaryAssembly(current.ContainingAssembly, primaryAssembly))
                    {
                        continue;
                    }

                    return false;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool IsAccessibleToGeneratedMediator(ISymbol symbol, IAssemblySymbol primaryAssembly)
    {
        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => true,
            Accessibility.Internal or Accessibility.ProtectedOrInternal => HasInternalAccessToPrimaryAssembly(symbol.ContainingAssembly, primaryAssembly),
            _ => false,
        };
    }

    private static bool HasInternalAccessToPrimaryAssembly(IAssemblySymbol assembly, IAssemblySymbol primaryAssembly)
    {
        return SymbolEqualityComparer.Default.Equals(assembly, primaryAssembly)
               || assembly.GivesAccessTo(primaryAssembly);
    }

    private static void ReportInaccessibleRegistrationType(
        INamedTypeSymbol type,
        bool isAccessibleToGeneratedMediator,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        if (isAccessibleToGeneratedMediator)
        {
            return;
        }

        var metadataName = GetMetadataName(type);
        if (!discoveryState.DiagnosedInaccessibleTypes.Add(metadataName))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? type.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(PrimaryAssemblyNamePropertyName, primaryAssembly.Identity.Name);
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.InaccessibleRegistrationType,
                GetDiagnosticLocation(location, SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, primaryAssembly)),
                properties,
                metadataName));
    }

    private static void ReportUnmarkedReferencedAssemblies(
        Compilation compilation,
        DiscoverySymbols discoverySymbols,
        INamedTypeSymbol? moduleAttribute,
        DiscoveryState discoveryState,
        CancellationToken cancellationToken)
    {
        if (moduleAttribute is null)
        {
            return;
        }

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasModuleMarker(referencedAssembly, moduleAttribute)
                || !discoveryState.DiagnosedUnmarkedAssemblies.Add(referencedAssembly.Identity.Name)
                || !TryFindModuleCandidateLocation(referencedAssembly.GlobalNamespace, discoverySymbols, cancellationToken, out _))
            {
                continue;
            }

            discoveryState.Diagnostics.Add(
                Diagnostic.Create(
                    MediatorDiagnosticDescriptors.MissingModuleMarker,
                    Location.None,
                    referencedAssembly.Identity.Name));
            discoveryState.Diagnostics.Add(
                Diagnostic.Create(
                    MediatorDiagnosticDescriptors.UnprovenObjectDispatchCoverage,
                    Location.None,
                    referencedAssembly.Identity.Name));
        }
    }

    private static bool TryFindModuleCandidateLocation(
        INamespaceSymbol @namespace,
        DiscoverySymbols discoverySymbols,
        CancellationToken cancellationToken,
        out Location? location)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
        {
            if (TryFindModuleCandidateLocation(nestedNamespace, discoverySymbols, cancellationToken, out location))
            {
                return true;
            }
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            if (TryFindModuleCandidateLocation(type, discoverySymbols, cancellationToken, out location))
            {
                return true;
            }
        }

        location = null;
        return false;
    }

    private static bool TryFindModuleCandidateLocation(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        CancellationToken cancellationToken,
        out Location? location)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ContainsModuleCandidate(type, discoverySymbols))
        {
            location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? type.Locations.FirstOrDefault();
            return location is not null;
        }

        foreach (var nestedType in type.GetTypeMembers())
        {
            if (TryFindModuleCandidateLocation(nestedType, discoverySymbols, cancellationToken, out location))
            {
                return true;
            }
        }

        location = null;
        return false;
    }

    private static bool ContainsModuleCandidate(INamedTypeSymbol type, DiscoverySymbols discoverySymbols)
    {
        if (!IsDiscoverableType(type) || !type.TypeParameters.IsEmpty)
        {
            return false;
        }

        if (FindBestRequestInterface(type, discoverySymbols) is not null
            || ImplementsInterface(type, discoverySymbols.NotificationInterface)
            || ImplementsInterface(type, discoverySymbols.StreamRequestInterface))
        {
            return true;
        }

        return type.AllInterfaces.Any(
            candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.HandlerInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.CommandHandlerInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.CommandHandlerOfResponseInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.QueryHandlerInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.PipelineInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.NotificationHandlerInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.StreamHandlerInterface)
                         || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, discoverySymbols.StreamPipelineInterface));
    }

    private static void ReportDuplicateGeneratedRegistration(
        INamedTypeSymbol type,
        string serviceType,
        string implementationType,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        var registrationKey = $"{serviceType}|{implementationType}";
        if (discoveryState.GeneratedRegistrationKeys.Add(registrationKey))
        {
            return;
        }

        if (!discoveryState.DiagnosedDuplicateRegistrationKeys.Add(registrationKey))
        {
            return;
        }

        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.DuplicateGeneratedRegistration,
                GetDiagnosticLocation(type, primaryAssembly),
                serviceType,
                implementationType));
    }
}
