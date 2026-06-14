using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Diagnostic reporting helpers and shared type utilities used across discovery phases.
/// </summary>
internal static partial class DiscoveryModelBuilder
{
    private static void ReportStreamRequestHandlerDiagnostics(
        IDictionary<string, RawStreamRequestContract> streamRequestContracts,
        IEnumerable<StreamHandlerDescriptor> streamHandlers,
        DiscoveryState discoveryState)
    {
        var handlersByRequest = streamHandlers
            .GroupBy(static handler => handler.RequestMetadataName, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);

        foreach (var entry in streamRequestContracts)
        {
            var metadataName = entry.Key;
            var contract = entry.Value;
            var handlers = handlersByRequest.TryGetValue(metadataName, out var found) ? found : [];
            var compatibleAccessibleHandlerCount = handlers.Count(
                static handler => handler.IsAccessibleToGeneratedMediator && handler.HasCompatibleHandleMethod);

            var location = GetDiagnosticLocation(contract.Location, contract.IsInPrimaryAssembly);

            if (compatibleAccessibleHandlerCount == 0)
            {
                discoveryState.Diagnostics.Add(
                    Diagnostic.Create(MediatorDiagnosticDescriptors.MissingHandler, location, metadataName));
            }
            else if (compatibleAccessibleHandlerCount > 1)
            {
                discoveryState.Diagnostics.Add(
                    Diagnostic.Create(MediatorDiagnosticDescriptors.MultipleHandlers, location, metadataName, compatibleAccessibleHandlerCount));
            }
        }
    }

    private static void ReportRequestHandlerDiagnostics(
        IEnumerable<RequestDescriptor> requests,
        DiscoveryState discoveryState)
    {
        foreach (var request in requests)
        {
            var compatibleAccessibleHandlerCount = request.Handlers.Count(
                static handler => handler.IsAccessibleToGeneratedMediator && handler.HasCompatibleHandleMethod);

            if (compatibleAccessibleHandlerCount == 0)
            {
                ReportRequestDiagnostic(MediatorDiagnosticDescriptors.MissingHandler, request, compatibleAccessibleHandlerCount, discoveryState);
                continue;
            }

            if (compatibleAccessibleHandlerCount > 1)
            {
                ReportRequestDiagnostic(MediatorDiagnosticDescriptors.MultipleHandlers, request, compatibleAccessibleHandlerCount, discoveryState);
            }
        }
    }

    private static void ReportDuplicateGeneratedRegistrationDiagnostics(DiscoveryState discoveryState)
    {
        foreach (var duplicate in discoveryState.DuplicateRegistrationDiagnostics.Values
                     .OrderBy(static (DuplicateRegistrationDiagnostic duplicate) => duplicate.ServiceType, StringComparer.Ordinal)
                     .ThenBy(static duplicate => duplicate.ImplementationType, StringComparer.Ordinal))
        {
            discoveryState.Diagnostics.Add(
                Diagnostic.Create(
                    MediatorDiagnosticDescriptors.DuplicateGeneratedRegistration,
                    duplicate.Location,
                    duplicate.ServiceType,
                    duplicate.ImplementationType));
        }
    }

    private static void ReportRequestDiagnostic(
        DiagnosticDescriptor descriptor,
        RequestDescriptor request,
        int handlerCount,
        DiscoveryState discoveryState)
    {
        if (!discoveryState.RequestContracts.TryGetValue(request.MetadataName, out var requestContract))
        {
            return;
        }

        var location = GetDiagnosticLocation(requestContract.Location, requestContract.IsInPrimaryAssembly);

        var diagnostic = descriptor.Id == MediatorDiagnosticIds.MissingHandler
            ? Diagnostic.Create(descriptor, location, request.MetadataName)
            : Diagnostic.Create(descriptor, location, request.MetadataName, handlerCount);
        discoveryState.Diagnostics.Add(diagnostic);
    }

    private static void ReportInvalidHandlerSignature(
        INamedTypeSymbol type,
        string requestMetadataName,
        bool hasCompatibleHandleMethod,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        if (hasCompatibleHandleMethod)
        {
            return;
        }

        var key = $"{GetMetadataName(type)}|{requestMetadataName}";
        if (!discoveryState.DiagnosedInvalidHandlerKeys.Add(key))
        {
            return;
        }

        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.InvalidHandlerSignature,
                GetDiagnosticLocation(type, primaryAssembly),
                GetMetadataName(type),
                requestMetadataName));
    }

    private static Location GetDiagnosticLocation(INamedTypeSymbol type, IAssemblySymbol primaryAssembly)
    {
        var location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? type.Locations.FirstOrDefault();
        return GetDiagnosticLocation(location, SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, primaryAssembly));
    }

    private static Location GetDiagnosticLocation(Location? location, bool isInPrimaryAssembly)
    {
        if (!isInPrimaryAssembly || location is null)
        {
            return Location.None;
        }

        return location;
    }


    private static bool IsDiscoverableType(INamedTypeSymbol type)
    {
        return type.TypeKind is not TypeKind.Interface
               && !type.IsAbstract
               && !type.IsImplicitlyDeclared;
    }

    private static INamedTypeSymbol? FindBestRequestInterface(INamedTypeSymbol type, DiscoverySymbols discoverySymbols)
    {
        if (type.AllInterfaces.SingleOrDefault(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.QueryInterface)) is { } queryInterface)
        {
            return queryInterface;
        }

        if (type.AllInterfaces.SingleOrDefault(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.CommandOfResponseInterface)) is { } commandOfResponseInterface)
        {
            return commandOfResponseInterface;
        }

        if (ImplementsInterface(type, discoverySymbols.CommandInterface))
        {
            return type.AllInterfaces.Single(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.RequestInterface));
        }

        return type.AllInterfaces.SingleOrDefault(
            candidate => SymbolEqualityComparer.Default.Equals(
                candidate.OriginalDefinition,
                discoverySymbols.RequestInterface));
    }

    private static RequestKind GetRequestKind(INamedTypeSymbol type, DiscoverySymbols discoverySymbols)
    {
        if (ImplementsInterface(type, discoverySymbols.QueryInterface))
        {
            return RequestKind.Query;
        }

        if (ImplementsInterface(type, discoverySymbols.CommandOfResponseInterface))
        {
            return RequestKind.CommandWithResponse;
        }

        return ImplementsInterface(type, discoverySymbols.CommandInterface)
            ? RequestKind.Command
            : RequestKind.Request;
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
    {
        return type.AllInterfaces.Any(
            candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, interfaceSymbol)
                         || SymbolEqualityComparer.Default.Equals(candidate, interfaceSymbol));
    }

    private static ResponseDescriptor CreateResponseDescriptor(ITypeSymbol type)
    {
        var namedType = type as INamedTypeSymbol;
        var isConstructedGenericType = namedType is { IsGenericType: true, TypeArguments.Length: > 0 };

        return new ResponseDescriptor(
            GetTypeDisplayString(type),
            isConstructedGenericType,
            isConstructedGenericType ? GetMetadataName(namedType!.OriginalDefinition) : null,
            isConstructedGenericType
                ? [.. namedType!.TypeArguments.Select(GetTypeDisplayString)]
                : [],
            [.. type.AllInterfaces
                .Select(GetTypeDisplayString)
                .OrderBy(static interfaceName => interfaceName, StringComparer.Ordinal)]);
    }

    private static bool HasCompatibleRequestHandleMethod(
        INamedTypeSymbol type,
        ITypeSymbol requestType,
        ITypeSymbol responseType,
        DiscoverySymbols discoverySymbols,
        IAssemblySymbol primaryAssembly)
    {
        return type.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Any(
                method =>
                    !method.IsStatic
                    && method.MethodKind == MethodKind.Ordinary
                    && method.Parameters.Length == 2
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, discoverySymbols.CancellationTokenType)
                    && SymbolEqualityComparer.Default.Equals(method.ReturnType, ConstructValueTaskResponse(discoverySymbols, responseType))
                    && IsAccessibleToGeneratedMediator(method, primaryAssembly));
    }

    private static INamedTypeSymbol ConstructValueTaskResponse(DiscoverySymbols discoverySymbols, ITypeSymbol responseType)
    {
        return discoverySymbols.ValueTaskOfT.Construct(responseType);
    }

    private static bool HasCompatibleStreamHandleMethod(
        INamedTypeSymbol type,
        ITypeSymbol requestType,
        ITypeSymbol responseType,
        DiscoverySymbols discoverySymbols,
        IAssemblySymbol primaryAssembly)
    {
        var expectedReturnType = discoverySymbols.AsyncEnumerableOfT.Construct(responseType);
        return type.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Any(
                method =>
                    !method.IsStatic
                    && method.MethodKind == MethodKind.Ordinary
                    && method.Parameters.Length == 2
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, discoverySymbols.CancellationTokenType)
                    && SymbolEqualityComparer.Default.Equals(method.ReturnType, expectedReturnType)
                    && IsAccessibleToGeneratedMediator(method, primaryAssembly));
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetNamespace(INamedTypeSymbol type)
    {
        return type.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : type.ContainingNamespace.ToDisplayString();
    }

    private static string GetTypeDisplayString(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetHandleMethodName(INamedTypeSymbol type)
    {
        return type.GetMembers()
                   .OfType<IMethodSymbol>()
                   .FirstOrDefault(static method => method.Name == "Handle")?
                   .Name
               ?? "Handle";
    }

}
