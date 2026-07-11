using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Domain.SourceGenerator;

/// <summary>
/// Emits identity equality support for models that explicitly opt in.
/// </summary>
[Generator]
public sealed class ModelIdentityGenerator : IIncrementalGenerator
{
    private const string AttributeName = "SharedKernel.Domain.GenerateModelSupportAttribute";
    private const string DefaultsAttributeName = "SharedKernel.Domain.GenerateModelSupportDefaultsAttribute";
    private const string IdentifiedInterfaceName = "SharedKernel.Domain.IIdentified<TId>";
    private const string IdentityOptionName = "Identity";
    private const string DiagnosticCategory = "SharedKernel.Domain.ModelSupport";
    private const string OpenBlock8 = "        {";
    private const string CloseBlock8 = "        }";
    private const string ReturnFalse12 = "            return false;";

    private static readonly DiagnosticDescriptor MissingPartial = new(
        "SKMDL001",
        "Identity generation requires a partial class",
        "Identity generation requested for '{0}', but the type is not partial",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingIdentifiedInterface = new(
        "SKMDL002",
        "Identity generation requires IIdentified<TId>",
        "Identity generation requested for '{0}', but the type does not implement IIdentified<TId>",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingId = new(
        "SKMDL003",
        "Identity generation requires a readable Id property",
        "Identity generation requested for '{0}', but the type does not expose a readable Id property",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MismatchedId = new(
        "SKMDL004",
        "Identity generation requires matching Id type",
        "Identity generation requested for '{0}', but Id type '{1}' does not match IIdentified<TId> type '{2}'",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedInheritance = new(
        "SKMDL005",
        "Identity generation does not support inherited models",
        "Identity generation requested for '{0}', but the type inherits from '{1}'",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedNestedType = new(
        "SKMDL006",
        "Identity generation does not support nested models",
        "Identity generation requested for '{0}', but nested types are not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedIdShape = new(
        "SKMDL007",
        "Identity generation requires a stable Id property",
        "Identity generation requested for '{0}', but Id must be an instance property with no setter or a private setter/init-only setter",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedDeclaration = new(
        "SKMDL008",
        "Identity generation supports class declarations only",
        "Identity generation requested for '{0}', but only class declarations are supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedGenericType = new(
        "SKMDL009",
        "Identity generation does not support generic models",
        "Identity generation requested for '{0}', but generic types are not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attributedModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, cancellationToken) => BuildModel(
                    (TypeDeclarationSyntax)attributeContext.TargetNode,
                    (INamedTypeSymbol)attributeContext.TargetSymbol,
                    attributeContext.SemanticModel.Compilation,
                    cancellationToken))
            .Where(static model => model.TypeName is not null || model.Diagnostic is not null)
            .Collect();

        var defaultCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                static (syntaxContext, _) => (TypeDeclarationSyntax)syntaxContext.Node)
            .Collect();

        var defaultModels = context.CompilationProvider
            .Combine(defaultCandidates)
            .Select(static (source, cancellationToken) => BuildDefaultModels(source.Left, source.Right, cancellationToken));

        var models = attributedModels
            .Combine(defaultModels)
            .Select(static (source, _) => source.Left.AddRange(source.Right))
            .WithTrackingName("ModelIdentityGenerationModels");

        context.RegisterSourceOutput(
            models,
            static (productionContext, models) =>
            {
                var generatedHintNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (var model in models)
                {
                    if (model.Diagnostic is not null)
                    {
                        productionContext.ReportDiagnostic(model.Diagnostic);
                        continue;
                    }

                    if (!generatedHintNames.Add(model.HintName!))
                    {
                        continue;
                    }

                    productionContext.AddSource(
                        model.HintName!,
                        SourceText.From(EmitModelSupport(model), Encoding.UTF8));
                }
            });
    }

    private static ImmutableArray<(string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic)> BuildDefaultModels(
        Compilation compilation,
        ImmutableArray<TypeDeclarationSyntax> candidates,
        CancellationToken cancellationToken)
    {
        if (!GetDefaults(compilation).Identity)
        {
            return ImmutableArray<(string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic)>.Empty;
        }

        var models = ImmutableArray.CreateBuilder<(string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic)>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var semanticModel = compilation.GetSemanticModel(candidate.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(candidate, cancellationToken) is not INamedTypeSymbol type)
            {
                continue;
            }

            if (HasGenerateModelSupportAttribute(type.GetAttributes()))
            {
                continue;
            }

            var model = BuildModel(candidate, type, compilation, cancellationToken);
            if (model.TypeName is not null || model.Diagnostic is not null)
            {
                models.Add(model);
            }
        }

        return models.ToImmutable();
    }

    private static bool HasGenerateModelSupportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.Any(static attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), AttributeName, StringComparison.Ordinal));
    }

    private static (string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic) BuildModel(
        TypeDeclarationSyntax typeDeclaration,
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identityOption = GetIdentityOption(type.GetAttributes());
        var identifiedInterface = type.AllInterfaces.FirstOrDefault(static interfaceType =>
            string.Equals(interfaceType.OriginalDefinition.ToDisplayString(), IdentifiedInterfaceName, StringComparison.Ordinal));
        var defaults = GetDefaults(compilation);
        var requestsIdentity = identityOption is true ||
            (identityOption is not false && defaults.Identity && identifiedInterface is not null);
        if (!requestsIdentity)
        {
            return default;
        }

        var location = typeDeclaration.Identifier.GetLocation();
        if (typeDeclaration is not ClassDeclarationSyntax)
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedDeclaration, location, type.Name));
        }

        if (type.ContainingType is not null)
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedNestedType, location, type.Name));
        }

        if (type.TypeParameters.Length > 0)
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedGenericType, location, type.Name));
        }

        if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return DiagnosticOnly(Diagnostic.Create(MissingPartial, location, type.Name));
        }

        if (type.BaseType is { SpecialType: not SpecialType.System_Object })
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedInheritance, location, type.Name, type.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        if (identifiedInterface is null)
        {
            return DiagnosticOnly(Diagnostic.Create(MissingIdentifiedInterface, location, type.Name));
        }

        var idProperty = type.GetMembers("Id").OfType<IPropertySymbol>().FirstOrDefault(static property => property.GetMethod is not null);
        if (idProperty is null)
        {
            return DiagnosticOnly(Diagnostic.Create(MissingId, location, type.Name));
        }

        if (!IsSupportedIdShape(idProperty))
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedIdShape, location, type.Name));
        }

        var idType = identifiedInterface.TypeArguments[0];
        var namespaceName = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();

        return !SymbolEqualityComparer.Default.Equals(idProperty.Type, idType)
            ? DiagnosticOnly(Diagnostic.Create(
                MismatchedId,
                location,
                type.Name,
                idProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                idType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
            : (
                namespaceName,
                type.Name,
                GetAccessibility(type.DeclaredAccessibility),
                idType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GetHintName(type),
                null);
    }

    private static (string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic) DiagnosticOnly(Diagnostic diagnostic)
    {
        return (null, null, null, null, null, diagnostic);
    }

    private static bool? GetIdentityOption(ImmutableArray<AttributeData> attributes)
    {
        var explicitFalse = false;
        foreach (var attribute in attributes)
        {
            if (!string.Equals(attribute.AttributeClass?.ToDisplayString(), AttributeName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (!string.Equals(argument.Key, IdentityOptionName, StringComparison.Ordinal) || argument.Value.Value is not bool value)
                {
                    continue;
                }

                if (value)
                {
                    return true;
                }

                explicitFalse = true;
            }
        }

        return explicitFalse ? false : null;
    }

    private static ModelSupportDefaults GetDefaults(Compilation compilation)
    {
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (!string.Equals(attribute.AttributeClass?.ToDisplayString(), DefaultsAttributeName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (string.Equals(argument.Key, IdentityOptionName, StringComparison.Ordinal) && argument.Value.Value is bool identity)
                {
                    return new ModelSupportDefaults(identity);
                }
            }
        }

        return default;
    }

    private static bool IsSupportedIdShape(IPropertySymbol idProperty)
    {
        return !idProperty.IsStatic &&
            (idProperty.SetMethod is null || idProperty.SetMethod.DeclaredAccessibility == Accessibility.Private);
    }

    private static string GetHintName(INamedTypeSymbol type)
    {
        var metadataName = type.ContainingNamespace.IsGlobalNamespace
            ? type.Name
            : $"{type.ContainingNamespace}.{type.Name}";
        var builder = new StringBuilder(metadataName.Length + ".ModelSupport.g.cs".Length);

        foreach (var character in metadataName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '.');
        }

        builder.Append(".ModelSupport.g.cs");
        return builder.ToString();
    }

    private static string EmitModelSupport((string? NamespaceName, string? TypeName, string? Accessibility, string? IdTypeName, string? HintName, Diagnostic? Diagnostic) model)
    {
        var builder = new StringBuilder("""
            // <auto-generated />
            #nullable enable

            """);

        if (model.NamespaceName is not null)
        {
            builder.Append("namespace ").Append(model.NamespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder
            .Append(model.Accessibility).Append(" partial class ").AppendLine(model.TypeName)
            .AppendLine("{")
            .AppendLine("    /// <inheritdoc />")
            .AppendLine("    public override bool Equals(object? obj)")
            .AppendLine("    {")
            .Append("        if (obj is not ").Append(model.TypeName).AppendLine(" other)")
            .AppendLine(OpenBlock8)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        if (ReferenceEquals(this, other))")
            .AppendLine(OpenBlock8)
            .AppendLine("            return true;")
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        if (GetType() != other.GetType())")
            .AppendLine(OpenBlock8)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .Append("        if (global::System.Collections.Generic.EqualityComparer<").Append(model.IdTypeName).AppendLine(">.Default.Equals(Id, default!) ||")
            .Append("            global::System.Collections.Generic.EqualityComparer<").Append(model.IdTypeName).AppendLine(">.Default.Equals(other.Id, default!))")
            .AppendLine(OpenBlock8)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .Append("        return global::System.Collections.Generic.EqualityComparer<").Append(model.IdTypeName).AppendLine(">.Default.Equals(Id, other.Id);")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    /// <inheritdoc />")
            .AppendLine("    public override int GetHashCode()")
            .AppendLine("    {")
            .Append("        if (global::System.Collections.Generic.EqualityComparer<").Append(model.IdTypeName).AppendLine(">.Default.Equals(Id, default!))")
            .AppendLine(OpenBlock8)
            .AppendLine("            return base.GetHashCode();")
            .AppendLine(CloseBlock8)
            .AppendLine()
            .Append("        return global::System.Collections.Generic.EqualityComparer<").Append(model.IdTypeName).AppendLine(">.Default.GetHashCode(Id);")
            .AppendLine("    }")
            .AppendLine("}");

        return builder.ToString();
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "private",
        };
    }

    private readonly struct ModelSupportDefaults(bool identity)
    {
        public bool Identity { get; } = identity;
    }
}
