using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Domain.SourceGenerator;

/// <summary>
/// Emits scalar value-object support for types that explicitly opt in.
/// </summary>
[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string AttributeName = "SharedKernel.Domain.GenerateValueObjectAttribute";
    private const string JsonConverterMetadataName = "System.Text.Json.Serialization.JsonConverter`1";
    private const string EfCoreValueConverterMetadataName = "Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2";
    private const string UnderlyingTypeOptionName = "UnderlyingType";
    private const string ParsingOptionName = "Parsing";
    private const string JsonOptionName = "Json";
    private const string EfCoreOptionName = "EfCore";
    private const string TemplateOptionName = "Template";
    private const string DiagnosticCategory = "SharedKernel.Domain.ModelSupport";
    private const string UnsupportedUnderlyingKindMessage = "Unsupported underlying kind.";
    private const string SummaryStartLine = "    /// <summary>";
    private const string SummaryEndLine = "    /// </summary>";
    private const string OpenBlock4 = "    {";
    private const string CloseBlock4 = "    }";
    private const string OpenBlock8 = "        {";
    private const string CloseBlock8 = "        }";
    private const string OpenBlock12 = "            {";
    private const string CloseBlock12 = "            }";
    private const string Indent12 = "            ";
    private const string Indent16 = "                ";
    private const string ResultDefault12 = "            result = default;";
    private const string ReturnFalse12 = "            return false;";
    private const string ReturnTrue8 = "        return true;";
    private const string ReturnParsedValue16 = "                return parsedValue;";
    private const string If12Prefix = "            if (";
    private const string StringTokenGuard12 = "            if (reader.TokenType != global::System.Text.Json.JsonTokenType.String)";
    private const string RecordStructDeclaration = " readonly partial record struct ";
    private const string ValueParameterSuffix = " value)";
    private const string JsonExceptionSuffixLine = ".\");";
    private static readonly DiagnosticDescriptor MissingUnderlyingType = new(
        "SKMDL010",
        "Value-object generation requires an underlying type",
        "Value-object generation requested for '{0}', but UnderlyingType was not set",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedDeclaration = new(
        "SKMDL011",
        "Value-object generation supports readonly record structs only",
        "Value-object generation requested for '{0}', but only readonly record structs are supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingPartial = new(
        "SKMDL012",
        "Value-object generation requires a partial declaration",
        "Value-object generation requested for '{0}', but the type is not partial",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedNestedType = new(
        "SKMDL013",
        "Value-object generation does not support nested types",
        "Value-object generation requested for '{0}', but nested types are not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedGenericType = new(
        "SKMDL014",
        "Value-object generation does not support generic types",
        "Value-object generation requested for '{0}', but generic types are not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedUnderlyingType = new(
        "SKMDL015",
        "Value-object generation requires a supported scalar type",
        "Value-object generation requested for '{0}', but underlying type '{1}' is not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingJsonReference = new(
        "SKMDL016",
        "JSON value-object generation requires System.Text.Json",
        "JSON value-object generation requested for '{0}', but System.Text.Json is not referenced",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingEfCoreReference = new(
        "SKMDL017",
        "EF Core value-object generation requires Microsoft.EntityFrameworkCore",
        "EF Core value-object generation requested for '{0}', but Microsoft.EntityFrameworkCore is not referenced",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedTemplate = new(
        "SKMDL018",
        "Value-object template does not support the selected underlying type",
        "Value-object generation requested for '{0}', but template '{1}' does not support underlying type '{2}'",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedConstructor = new(
        "SKMDL019",
        "Value-object generation does not support explicit constructors",
        "Value-object generation requested for '{0}', but explicit constructors can bypass generated validation",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ReservedMemberName = new(
        "SKMDL020",
        "Value-object generation requires generated member names to be unused",
        "Value-object generation requested for '{0}', but member '{1}' is generated by the value-object generator",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConflictingValueObjectConfiguration = new(
        "SKMDL021",
        "Value-object generation requires one attribute configuration per type",
        "Value-object generation requested for '{0}' with conflicting attribute configuration",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, cancellationToken) => BuildModel(attributeContext, cancellationToken))
            .Where(static model => model.TypeName is not null || model.Diagnostic is not null)
            .Collect()
            .WithTrackingName("ValueObjectGenerationModels");

        context.RegisterSourceOutput(
            models,
            static (productionContext, models) =>
            {
                var generatedModels = new Dictionary<string, ValueObjectModel>(StringComparer.Ordinal);

                foreach (var model in models)
                {
                    if (model.Diagnostic is not null)
                    {
                        productionContext.ReportDiagnostic(model.Diagnostic);
                        continue;
                    }

                    if (generatedModels.TryGetValue(model.CoreHintName!, out var existingModel))
                    {
                        if (!HasSameGenerationOptions(existingModel, model))
                        {
                            productionContext.ReportDiagnostic(Diagnostic.Create(
                                ConflictingValueObjectConfiguration,
                                model.Location,
                                model.TypeName));
                        }

                        continue;
                    }

                    generatedModels.Add(model.CoreHintName!, model);

                    productionContext.AddSource(model.CoreHintName!, SourceText.From(EmitValueObject(model), Encoding.UTF8));

                    if (model.Json)
                    {
                        productionContext.AddSource(model.JsonHintName!, SourceText.From(EmitJsonConverter(model), Encoding.UTF8));
                    }

                    if (model.EfCore)
                    {
                        productionContext.AddSource(model.EfCoreHintName!, SourceText.From(EmitEfCoreConverter(model), Encoding.UTF8));
                    }
                }
            });
    }

    private static ValueObjectModel BuildModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var typeDeclaration = (TypeDeclarationSyntax)context.TargetNode;
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var location = typeDeclaration.Identifier.GetLocation();

        if (!IsReadonlyRecordStruct(typeDeclaration, type))
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

        if (type.InstanceConstructors.Any(static constructor => !constructor.IsImplicitlyDeclared))
        {
            return DiagnosticOnly(Diagnostic.Create(UnsupportedConstructor, location, type.Name));
        }

        var attributes = context.Attributes
            .Where(static attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), AttributeName, StringComparison.Ordinal))
            .ToArray();
        var attribute = attributes[0];
        if (attributes.Skip(1).Any(candidate => !HasSameAttributeConfiguration(attribute, candidate)))
        {
            return DiagnosticOnly(Diagnostic.Create(ConflictingValueObjectConfiguration, location, type.Name));
        }

        var underlyingType = GetUnderlyingType(attribute);
        if (underlyingType is null)
        {
            return DiagnosticOnly(Diagnostic.Create(MissingUnderlyingType, location, type.Name));
        }

        var underlyingKind = GetUnderlyingKind(underlyingType);
        if (underlyingKind == UnderlyingKind.Unsupported)
        {
            return DiagnosticOnly(Diagnostic.Create(
                UnsupportedUnderlyingType,
                location,
                type.Name,
                underlyingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        var template = GetTemplate(attribute);
        if (!TemplateSupportsUnderlyingType(template, underlyingKind))
        {
            return DiagnosticOnly(Diagnostic.Create(
                UnsupportedTemplate,
                location,
                type.Name,
                template,
                underlyingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        var json = GetBooleanOption(attribute, JsonOptionName);
        if (json && context.SemanticModel.Compilation.GetTypeByMetadataName(JsonConverterMetadataName) is null)
        {
            return DiagnosticOnly(Diagnostic.Create(MissingJsonReference, location, type.Name));
        }

        var efCore = GetBooleanOption(attribute, EfCoreOptionName);
        if (efCore && context.SemanticModel.Compilation.GetTypeByMetadataName(EfCoreValueConverterMetadataName) is null)
        {
            return DiagnosticOnly(Diagnostic.Create(MissingEfCoreReference, location, type.Name));
        }

        var parsing = GetBooleanOption(attribute, ParsingOptionName) || template == ValueObjectTemplate.ApiVersion;
        if (HasReservedGeneratedMember(type, parsing, json, efCore, template, out var reservedMemberName))
        {
            return DiagnosticOnly(Diagnostic.Create(ReservedMemberName, location, type.Name, reservedMemberName));
        }

        var namespaceName = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        return new ValueObjectModel
        {
            NamespaceName = namespaceName,
            TypeName = type.Name,
            Accessibility = GetAccessibility(type.DeclaredAccessibility),
            UnderlyingTypeName = GetTypeName(underlyingKind),
            UnderlyingKind = underlyingKind,
            Parsing = parsing,
            Json = json,
            EfCore = efCore,
            Template = template,
            CoreHintName = GetHintName(type, "ValueObject"),
            JsonHintName = GetHintName(type, "Json"),
            EfCoreHintName = GetHintName(type, "EfCore"),
            Location = location,
        };
    }

    private static bool HasSameGenerationOptions(ValueObjectModel left, ValueObjectModel right)
    {
        return string.Equals(left.UnderlyingTypeName, right.UnderlyingTypeName, StringComparison.Ordinal) &&
            left.UnderlyingKind == right.UnderlyingKind &&
            left.Parsing == right.Parsing &&
            left.Json == right.Json &&
            left.EfCore == right.EfCore &&
            left.Template == right.Template;
    }

    private static bool HasSameAttributeConfiguration(AttributeData left, AttributeData right)
    {
        return SymbolEqualityComparer.Default.Equals(GetUnderlyingType(left), GetUnderlyingType(right)) &&
            GetBooleanOption(left, ParsingOptionName) == GetBooleanOption(right, ParsingOptionName) &&
            GetBooleanOption(left, JsonOptionName) == GetBooleanOption(right, JsonOptionName) &&
            GetBooleanOption(left, EfCoreOptionName) == GetBooleanOption(right, EfCoreOptionName) &&
            GetTemplate(left) == GetTemplate(right);
    }

    private static ValueObjectModel DiagnosticOnly(Diagnostic diagnostic)
    {
        return new ValueObjectModel { Diagnostic = diagnostic };
    }

    private static bool IsReadonlyRecordStruct(TypeDeclarationSyntax declaration, INamedTypeSymbol type)
    {
        return type.IsValueType &&
            type.IsRecord &&
            declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
    }

    private static ITypeSymbol? GetUnderlyingType(AttributeData attribute)
    {
        return attribute.NamedArguments
            .Where(static argument => string.Equals(argument.Key, UnderlyingTypeOptionName, StringComparison.Ordinal))
            .Select(static argument => argument.Value.Value as ITypeSymbol)
            .FirstOrDefault();
    }

    private static bool GetBooleanOption(AttributeData attribute, string optionName)
    {
        return attribute.NamedArguments
            .Where(argument => string.Equals(argument.Key, optionName, StringComparison.Ordinal))
            .Select(static argument => argument.Value.Value)
            .OfType<bool>()
            .FirstOrDefault();
    }

    private static ValueObjectTemplate GetTemplate(AttributeData attribute)
    {
        return attribute.NamedArguments
            .Where(static argument => string.Equals(argument.Key, TemplateOptionName, StringComparison.Ordinal))
            .Select(static argument => argument.Value.Value)
            .OfType<int>()
            .Select(static template => (ValueObjectTemplate)template)
            .FirstOrDefault();
    }

    private static UnderlyingKind GetUnderlyingKind(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return UnderlyingKind.String;
        }

        if (type.SpecialType == SpecialType.System_Int32)
        {
            return UnderlyingKind.Int32;
        }

        if (type.SpecialType == SpecialType.System_Decimal)
        {
            return UnderlyingKind.Decimal;
        }

        var metadataName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return metadataName switch
        {
            "global::System.Guid" => UnderlyingKind.Guid,
            "global::System.DateOnly" => UnderlyingKind.DateOnly,
            _ => UnderlyingKind.Unsupported,
        };
    }

    private static bool HasReservedGeneratedMember(
        INamedTypeSymbol type,
        bool parsing,
        bool json,
        bool efCore,
        ValueObjectTemplate template,
        out string memberName)
    {
        var reservedMemberNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Create",
            "IsValid",
            "ToString",
            "TryCreate",
            "Value",
        };

        if (parsing)
        {
            reservedMemberNames.Add("Parse");
            reservedMemberNames.Add("TryParse");
        }

        if (json)
        {
            reservedMemberNames.Add("JsonConverter");
        }

        if (efCore)
        {
            reservedMemberNames.Add("EfCoreValueConverter");
        }

        if (template == ValueObjectTemplate.ApiVersion)
        {
            reservedMemberNames.Add("CompareTo");
            reservedMemberNames.Add("ToRouteSegment");
        }
        else if (template == ValueObjectTemplate.Slug)
        {
            reservedMemberNames.Add("IsSlug");
        }
        else if (template == ValueObjectTemplate.IsoCode)
        {
            reservedMemberNames.Add("IsIsoCode");
        }

        var reservedMember = type.GetMembers()
            .FirstOrDefault(member =>
                !member.IsImplicitlyDeclared &&
                member.DeclaringSyntaxReferences.Length > 0 &&
                reservedMemberNames.Contains(member.Name));
        if (reservedMember is not null)
        {
            memberName = reservedMember.Name;
            return true;
        }

        memberName = string.Empty;
        return false;
    }

    private static bool TemplateSupportsUnderlyingType(ValueObjectTemplate template, UnderlyingKind underlyingKind)
    {
        return template switch
        {
            ValueObjectTemplate.None => true,
            ValueObjectTemplate.ApiVersion => underlyingKind == UnderlyingKind.Int32,
            ValueObjectTemplate.NonEmptyString => underlyingKind == UnderlyingKind.String,
            ValueObjectTemplate.Slug => underlyingKind == UnderlyingKind.String,
            ValueObjectTemplate.StronglyTypedId => underlyingKind is UnderlyingKind.Guid or UnderlyingKind.Int32,
            ValueObjectTemplate.IsoCode => underlyingKind == UnderlyingKind.String,
            _ => false,
        };
    }

    private static string GetTypeName(UnderlyingKind underlyingKind)
    {
        return underlyingKind switch
        {
            UnderlyingKind.String => "string",
            UnderlyingKind.Guid => "global::System.Guid",
            UnderlyingKind.Int32 => "int",
            UnderlyingKind.Decimal => "decimal",
            UnderlyingKind.DateOnly => "global::System.DateOnly",
            _ => throw new InvalidOperationException(UnsupportedUnderlyingKindMessage),
        };
    }

    private static string GetHintName(INamedTypeSymbol type, string suffix)
    {
        var metadataName = type.ContainingNamespace.IsGlobalNamespace
            ? type.Name
            : $"{type.ContainingNamespace}.{type.Name}";
        var builder = new StringBuilder(metadataName.Length + suffix.Length + ".g.cs".Length + 1);

        foreach (var character in metadataName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '.');
        }

        builder.Append('.').Append(suffix).Append(".g.cs");
        return builder.ToString();
    }

    private static string EmitValueObject(ValueObjectModel model)
    {
        var builder = CreateHeader(model.NamespaceName);
        var interfaceClause = model.Template == ValueObjectTemplate.ApiVersion
            ? $" : global::System.IComparable<{model.TypeName}>"
            : string.Empty;

        builder
            .Append(model.Accessibility).Append(RecordStructDeclaration).Append(model.TypeName).AppendLine(interfaceClause)
            .AppendLine("{")
            .AppendLine(SummaryStartLine)
            .AppendLine("    /// Gets the underlying scalar value.")
            .AppendLine(SummaryEndLine)
            .Append("    public ").Append(model.UnderlyingTypeName).AppendLine(" Value { get; }")
            .AppendLine()
            .Append("    private ").Append(model.TypeName).Append('(').Append(model.UnderlyingTypeName).AppendLine(ValueParameterSuffix)
            .AppendLine(OpenBlock4)
            .AppendLine("        Value = value;")
            .AppendLine(CloseBlock4)
            .AppendLine()
            .AppendLine(SummaryStartLine)
            .AppendLine("    /// Creates a value object after validating the supplied scalar value.")
            .AppendLine(SummaryEndLine)
            .AppendLine("    /// <param name=\"value\">The scalar value.</param>")
            .AppendLine("    /// <returns>The generated value object.</returns>")
            .Append("    public static ").Append(model.TypeName).Append(" Create(").Append(model.UnderlyingTypeName).AppendLine(ValueParameterSuffix)
            .AppendLine(OpenBlock4)
            .AppendLine("        if (!TryCreate(value, out var result))")
            .AppendLine(OpenBlock8)
            .Append("            throw new global::System.ArgumentException(\"The value is not valid for ").Append(model.TypeName).AppendLine(".\", nameof(value));")
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        return result;")
            .AppendLine(CloseBlock4)
            .AppendLine()
            .AppendLine(SummaryStartLine)
            .AppendLine("    /// Attempts to create a value object after validating the supplied scalar value.")
            .AppendLine(SummaryEndLine)
            .AppendLine("    /// <param name=\"value\">The scalar value.</param>")
            .AppendLine("    /// <param name=\"result\">The generated value object when validation succeeds.</param>")
            .AppendLine("    /// <returns><see langword=\"true\" /> when the value is valid; otherwise, <see langword=\"false\" />.</returns>")
            .Append("    public static bool TryCreate(").Append(model.UnderlyingTypeName).Append(" value, out ").Append(model.TypeName).AppendLine(" result)")
            .AppendLine(OpenBlock4)
            .AppendLine("        if (!IsValid(value))")
            .AppendLine(OpenBlock8)
            .AppendLine(ResultDefault12)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .Append("        result = new ").Append(model.TypeName).AppendLine("(value);")
            .AppendLine(ReturnTrue8)
            .AppendLine(CloseBlock4)
            .AppendLine();

        if (model.Parsing)
        {
            AppendParsing(builder, model);
        }

        AppendFormatting(builder, model);
        AppendValidation(builder, model);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendParsing(StringBuilder builder, ValueObjectModel model)
    {
        builder
            .AppendLine(SummaryStartLine)
            .AppendLine("    /// Parses text into a generated value object.")
            .AppendLine(SummaryEndLine)
            .AppendLine("    /// <param name=\"text\">The text to parse.</param>")
            .AppendLine("    /// <param name=\"provider\">The format provider.</param>")
            .AppendLine("    /// <returns>The parsed value object.</returns>")
            .Append("    public static ").Append(model.TypeName).AppendLine(" Parse(string text, global::System.IFormatProvider? provider = null)")
            .AppendLine(OpenBlock4)
            .AppendLine("        if (!TryParse(text, provider, out var result))")
            .AppendLine(OpenBlock8)
            .Append("            throw new global::System.FormatException(\"The text is not a valid ").Append(model.TypeName).AppendLine(" value.\");")
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        return result;")
            .AppendLine(CloseBlock4)
            .AppendLine()
            .AppendLine(SummaryStartLine)
            .AppendLine("    /// Attempts to parse text into a generated value object.")
            .AppendLine(SummaryEndLine)
            .AppendLine("    /// <param name=\"text\">The text to parse.</param>")
            .AppendLine("    /// <param name=\"provider\">The format provider.</param>")
            .AppendLine("    /// <param name=\"result\">The parsed value object when parsing succeeds.</param>")
            .AppendLine("    /// <returns><see langword=\"true\" /> when parsing succeeds; otherwise, <see langword=\"false\" />.</returns>")
            .Append("    public static bool TryParse(string? text, global::System.IFormatProvider? provider, out ").Append(model.TypeName).AppendLine(" result)")
            .AppendLine(OpenBlock4);

        AppendTryParseBody(builder, model);

        builder
            .AppendLine(CloseBlock4)
            .AppendLine();
    }

    private static void AppendTryParseBody(StringBuilder builder, ValueObjectModel model)
    {
        if (model.Template == ValueObjectTemplate.ApiVersion)
        {
            builder
                .AppendLine("        if (string.IsNullOrWhiteSpace(text))")
                .AppendLine(OpenBlock8)
                .AppendLine(ResultDefault12)
                .AppendLine(ReturnFalse12)
                .AppendLine(CloseBlock8)
                .AppendLine()
                .AppendLine("        var normalized = global::System.MemoryExtensions.AsSpan(text);")
                .AppendLine("        if (global::System.MemoryExtensions.StartsWith(normalized, global::System.MemoryExtensions.AsSpan(\"v\"), global::System.StringComparison.OrdinalIgnoreCase))")
                .AppendLine(OpenBlock8)
                .AppendLine("            normalized = normalized.Slice(1);")
                .AppendLine(CloseBlock8)
                .AppendLine("        if (!int.TryParse(normalized, global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture, out var parsed))")
                .AppendLine(OpenBlock8)
                .AppendLine(ResultDefault12)
                .AppendLine(ReturnFalse12)
                .AppendLine(CloseBlock8)
                .AppendLine()
                .AppendLine("        return TryCreate(parsed, out result);");
            return;
        }

        switch (model.UnderlyingKind)
        {
            case UnderlyingKind.String:
                builder
                    .AppendLine("        if (text is null)")
                    .AppendLine(OpenBlock8)
                    .AppendLine(ResultDefault12)
                    .AppendLine(ReturnFalse12)
                    .AppendLine(CloseBlock8)
                    .AppendLine()
                    .AppendLine("        return TryCreate(text, out result);");
                break;
            case UnderlyingKind.Guid:
                AppendParsedTryCreate(builder, "global::System.Guid.TryParse(text, out var parsed)");
                break;
            case UnderlyingKind.Int32:
                AppendParsedTryCreate(builder, "int.TryParse(text, global::System.Globalization.NumberStyles.Integer, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out var parsed)");
                break;
            case UnderlyingKind.Decimal:
                AppendParsedTryCreate(builder, "decimal.TryParse(text, global::System.Globalization.NumberStyles.Number, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out var parsed)");
                break;
            case UnderlyingKind.DateOnly:
                AppendParsedTryCreate(builder, "global::System.DateOnly.TryParse(text, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed)");
                break;
            default:
                throw new InvalidOperationException(UnsupportedUnderlyingKindMessage);
        }
    }

    private static void AppendParsedTryCreate(StringBuilder builder, string parseExpression)
    {
        builder
            .Append("        if (!").Append(parseExpression).AppendLine(")")
            .AppendLine(OpenBlock8)
            .AppendLine(ResultDefault12)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        return TryCreate(parsed, out result);");
    }

    private static void AppendFormatting(StringBuilder builder, ValueObjectModel model)
    {
        builder
            .AppendLine("    /// <inheritdoc />")
            .AppendLine("    public override readonly string ToString()")
            .AppendLine(OpenBlock4);

        if (model.Template == ValueObjectTemplate.ApiVersion)
        {
            builder.AppendLine("        return ToRouteSegment();");
        }
        else
        {
            builder.Append("        return ").Append(GetFormatExpression(model, "Value")).AppendLine(";");
        }

        builder
            .AppendLine(CloseBlock4)
            .AppendLine();

        if (model.Template == ValueObjectTemplate.ApiVersion)
        {
            builder
                .AppendLine(SummaryStartLine)
                .AppendLine("    /// Formats the version as an API route segment.")
                .AppendLine(SummaryEndLine)
                .AppendLine("    /// <returns>The route segment, such as <c>v1</c>.</returns>")
                .AppendLine("    public readonly string ToRouteSegment()")
                .AppendLine(OpenBlock4)
                .AppendLine("        return \"v\" + Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);")
                .AppendLine(CloseBlock4)
                .AppendLine()
                .AppendLine("    /// <inheritdoc />")
                .Append("    public readonly int CompareTo(").Append(model.TypeName).AppendLine(" other)")
                .AppendLine(OpenBlock4)
                .AppendLine("        return Value.CompareTo(other.Value);")
                .AppendLine(CloseBlock4)
                .AppendLine();
        }
    }

    private static string GetFormatExpression(ValueObjectModel model, string valueExpression)
    {
        return model.UnderlyingKind switch
        {
            UnderlyingKind.String => $"{valueExpression} ?? string.Empty",
            UnderlyingKind.Guid => $"{valueExpression}.ToString()",
            UnderlyingKind.Int32 => $"{valueExpression}.ToString(global::System.Globalization.CultureInfo.InvariantCulture)",
            UnderlyingKind.Decimal => $"{valueExpression}.ToString(global::System.Globalization.CultureInfo.InvariantCulture)",
            UnderlyingKind.DateOnly => $"{valueExpression}.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)",
            _ => throw new InvalidOperationException(UnsupportedUnderlyingKindMessage),
        };
    }

    private static void AppendValidation(StringBuilder builder, ValueObjectModel model)
    {
        builder
            .Append("    static partial void ValidateValue(").Append(model.UnderlyingTypeName).AppendLine(" value, ref bool isValid);")
            .AppendLine()
            .Append("    private static bool IsValid(").Append(model.UnderlyingTypeName).AppendLine(ValueParameterSuffix)
            .AppendLine(OpenBlock4)
            .Append("        var isValid = ").Append(GetValidationExpression(model)).AppendLine(";")
            .AppendLine("        if (isValid)")
            .AppendLine(OpenBlock8)
            .AppendLine("            ValidateValue(value, ref isValid);")
            .AppendLine(CloseBlock8)
            .AppendLine("        return isValid;")
            .AppendLine(CloseBlock4);

        if (model.Template == ValueObjectTemplate.Slug)
        {
            AppendSlugValidator(builder);
        }
        else if (model.Template == ValueObjectTemplate.IsoCode)
        {
            AppendIsoCodeValidator(builder);
        }
    }

    private static string GetValidationExpression(ValueObjectModel model)
    {
        return model.Template switch
        {
            ValueObjectTemplate.ApiVersion => "value > 0",
            ValueObjectTemplate.NonEmptyString => "!string.IsNullOrWhiteSpace(value)",
            ValueObjectTemplate.Slug => "IsSlug(value)",
            ValueObjectTemplate.StronglyTypedId when model.UnderlyingKind == UnderlyingKind.Guid => "value != global::System.Guid.Empty",
            ValueObjectTemplate.StronglyTypedId => "value > 0",
            ValueObjectTemplate.IsoCode => "IsIsoCode(value)",
            _ when model.UnderlyingKind == UnderlyingKind.String => "value is not null",
            _ => "true",
        };
    }

    private static void AppendSlugValidator(StringBuilder builder)
    {
        builder
            .AppendLine()
            .AppendLine("    private static bool IsSlug(string value)")
            .AppendLine(OpenBlock4)
            .AppendLine("        if (string.IsNullOrWhiteSpace(value) || value[0] == '-' || value[value.Length - 1] == '-')")
            .AppendLine(OpenBlock8)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        foreach (var character in value)")
            .AppendLine(OpenBlock8)
            .AppendLine("            if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '-'))")
            .AppendLine(OpenBlock12)
            .AppendLine("                return false;")
            .AppendLine(CloseBlock12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine(ReturnTrue8)
            .AppendLine(CloseBlock4);
    }

    private static void AppendIsoCodeValidator(StringBuilder builder)
    {
        builder
            .AppendLine()
            .AppendLine("    private static bool IsIsoCode(string value)")
            .AppendLine(OpenBlock4)
            .AppendLine("        if (value is null || value.Length is < 2 or > 3)")
            .AppendLine(OpenBlock8)
            .AppendLine(ReturnFalse12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        foreach (var character in value)")
            .AppendLine(OpenBlock8)
            .AppendLine("            if (!((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z')))")
            .AppendLine(OpenBlock12)
            .AppendLine("                return false;")
            .AppendLine(CloseBlock12)
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine(ReturnTrue8)
            .AppendLine(CloseBlock4);
    }

    private static string EmitJsonConverter(ValueObjectModel model)
    {
        var builder = CreateHeader(model.NamespaceName);
        builder
            .Append(model.Accessibility).Append(RecordStructDeclaration).AppendLine(model.TypeName)
            .AppendLine("{")
            .AppendLine(SummaryStartLine)
            .Append("    /// Converts <see cref=\"").Append(model.TypeName).AppendLine("\" /> values for System.Text.Json.")
            .AppendLine(SummaryEndLine)
            .Append("    public sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<").Append(model.TypeName).AppendLine(">")
            .AppendLine(OpenBlock4)
            .AppendLine("        /// <inheritdoc />")
            .Append("        public override ").Append(model.TypeName).AppendLine(" Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)")
            .AppendLine(OpenBlock8);

        AppendJsonReadBody(builder, model);

        builder
            .AppendLine(CloseBlock8)
            .AppendLine()
            .AppendLine("        /// <inheritdoc />")
            .Append("        public override void Write(global::System.Text.Json.Utf8JsonWriter writer, ").Append(model.TypeName).AppendLine(" value, global::System.Text.Json.JsonSerializerOptions options)")
            .AppendLine(OpenBlock8);

        AppendJsonWriteBody(builder, model);

        builder
            .AppendLine(CloseBlock8)
            .AppendLine(CloseBlock4)
            .AppendLine("}");
        return builder.ToString();
    }

    private static void AppendJsonReadBody(StringBuilder builder, ValueObjectModel model)
    {
        if (model.Template == ValueObjectTemplate.ApiVersion)
        {
            builder
                .AppendLine("            if (reader.TokenType == global::System.Text.Json.JsonTokenType.Number)")
                .AppendLine(OpenBlock12)
                .Append("                if (reader.TryGetInt32(out var value) && ").Append(model.TypeName).AppendLine(".TryCreate(value, out var parsedNumber))")
                .AppendLine("                {")
                .AppendLine("                    return parsedNumber;")
                .AppendLine("                }")
                .AppendLine();
            AppendJsonInvalidThrow(builder, model, Indent16);

            builder
                .AppendLine(CloseBlock12)
                .AppendLine()
                .AppendLine(StringTokenGuard12)
                .AppendLine(OpenBlock12);
            AppendJsonInvalidThrow(builder, model, Indent16);

            builder
                .AppendLine(CloseBlock12)
                .AppendLine()
                .AppendLine("            var text = reader.GetString();")
                .Append("            if (").Append(model.TypeName).AppendLine(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var parsed))")
                .AppendLine(OpenBlock12)
                .AppendLine("                return parsed;")
                .AppendLine(CloseBlock12)
                .AppendLine();
            AppendJsonInvalidThrow(builder, model);
            return;
        }

        switch (model.UnderlyingKind)
        {
            case UnderlyingKind.String:
                builder
                    .AppendLine(StringTokenGuard12)
                    .AppendLine(OpenBlock12);
                AppendJsonInvalidThrow(builder, model, Indent16);

                builder
                    .AppendLine(CloseBlock12)
                    .AppendLine()
                    .AppendLine("            var value = reader.GetString();")
                    .AppendLine("            if (value is null)")
                    .AppendLine(OpenBlock12);
                AppendJsonInvalidThrow(builder, model, Indent16);

                builder
                    .AppendLine(CloseBlock12)
                    .AppendLine()
                    .Append(If12Prefix).Append(model.TypeName).AppendLine(".TryCreate(value, out var parsedValue))")
                    .AppendLine(OpenBlock12)
                    .AppendLine(ReturnParsedValue16)
                    .AppendLine(CloseBlock12)
                    .AppendLine();
                AppendJsonInvalidThrow(builder, model);
                break;
            case UnderlyingKind.Guid:
                AppendJsonTryGetAndCreateOrThrow(
                    builder,
                    model,
                    "global::System.Text.Json.JsonTokenType.String",
                    "reader.TryGetGuid(out var value)");
                break;
            case UnderlyingKind.Int32:
                AppendJsonTryGetAndCreateOrThrow(
                    builder,
                    model,
                    "global::System.Text.Json.JsonTokenType.Number",
                    "reader.TryGetInt32(out var value)");
                break;
            case UnderlyingKind.Decimal:
                AppendJsonTryGetAndCreateOrThrow(
                    builder,
                    model,
                    "global::System.Text.Json.JsonTokenType.Number",
                    "reader.TryGetDecimal(out var value)");
                break;
            case UnderlyingKind.DateOnly:
                builder
                    .AppendLine(StringTokenGuard12)
                    .AppendLine(OpenBlock12);
                AppendJsonInvalidThrow(builder, model, Indent16);

                builder
                    .AppendLine(CloseBlock12)
                    .AppendLine()
                    .AppendLine("            var value = reader.GetString();")
                    .AppendLine("            if (!global::System.DateOnly.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed))")
                    .AppendLine(OpenBlock12);
                AppendJsonInvalidThrow(builder, model, Indent16);

                builder
                    .AppendLine(CloseBlock12)
                    .AppendLine();
                AppendJsonTryCreateOrThrow(builder, model, "parsed");
                break;
            default:
                throw new InvalidOperationException(UnsupportedUnderlyingKindMessage);
        }
    }

    private static void AppendJsonWriteBody(StringBuilder builder, ValueObjectModel model)
    {
        builder
            .Append(If12Prefix).Append('!').Append(model.TypeName).AppendLine(".TryCreate(value.Value, out _))")
            .AppendLine(OpenBlock12);
        AppendJsonInvalidThrow(builder, model, Indent16);

        builder
            .AppendLine(CloseBlock12)
            .AppendLine();

        if (model.Template == ValueObjectTemplate.ApiVersion)
        {
            builder.AppendLine("            writer.WriteStringValue(value.ToRouteSegment());");
            return;
        }

        switch (model.UnderlyingKind)
        {
            case UnderlyingKind.String:
            case UnderlyingKind.Guid:
                builder.AppendLine("            writer.WriteStringValue(value.Value);");
                break;
            case UnderlyingKind.Int32:
            case UnderlyingKind.Decimal:
                builder.AppendLine("            writer.WriteNumberValue(value.Value);");
                break;
            case UnderlyingKind.DateOnly:
                builder.AppendLine("            writer.WriteStringValue(value.Value.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
                break;
            default:
                throw new InvalidOperationException(UnsupportedUnderlyingKindMessage);
        }
    }

    private static void AppendJsonTryGetAndCreateOrThrow(
        StringBuilder builder,
        ValueObjectModel model,
        string expectedTokenType,
        string tryGetExpression)
    {
        builder
            .Append(If12Prefix).Append("reader.TokenType == ").Append(expectedTokenType).Append(" && ").Append(tryGetExpression).Append(" && ")
            .Append(model.TypeName).AppendLine(".TryCreate(value, out var parsedValue))")
            .AppendLine(OpenBlock12)
            .AppendLine(ReturnParsedValue16)
            .AppendLine(CloseBlock12)
            .AppendLine();
        AppendJsonInvalidThrow(builder, model);
    }

    private static void AppendJsonTryCreateOrThrow(StringBuilder builder, ValueObjectModel model, string valueExpression)
    {
        builder
            .Append(If12Prefix).Append(model.TypeName).Append(".TryCreate(").Append(valueExpression).AppendLine(", out var parsedValue))")
            .AppendLine(OpenBlock12)
            .AppendLine(ReturnParsedValue16)
            .AppendLine(CloseBlock12)
            .AppendLine();
        AppendJsonInvalidThrow(builder, model);
    }

    private static void AppendJsonInvalidThrow(StringBuilder builder, ValueObjectModel model, string indent = Indent12)
    {
        builder.Append(indent).Append("throw new global::System.Text.Json.JsonException(\"The JSON value is not a valid ").Append(model.TypeName).AppendLine(JsonExceptionSuffixLine);
    }

    private static string EmitEfCoreConverter(ValueObjectModel model)
    {
        var builder = CreateHeader(model.NamespaceName);
        builder
            .Append(model.Accessibility).Append(RecordStructDeclaration).AppendLine(model.TypeName)
            .AppendLine("{")
            .AppendLine(SummaryStartLine)
            .Append("    /// Converts <see cref=\"").Append(model.TypeName).AppendLine("\" /> values for EF Core.")
            .AppendLine(SummaryEndLine)
            .AppendLine("    public sealed class EfCoreValueConverter")
            .Append("        : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<").Append(model.TypeName).Append(", ").Append(model.UnderlyingTypeName).AppendLine(">")
            .AppendLine(OpenBlock4)
            .AppendLine("        /// <summary>")
            .AppendLine("        /// Initializes a new instance of the <see cref=\"EfCoreValueConverter\" /> class.")
            .AppendLine("        /// </summary>")
            .AppendLine("        /// <param name=\"mappingHints\">Optional EF Core converter mapping hints.</param>")
            .AppendLine("        public EfCoreValueConverter(global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ConverterMappingHints? mappingHints = null)")
            .Append("            : base(value => value.Value, value => ").Append(model.TypeName).AppendLine(".Create(value), mappingHints)")
            .AppendLine(OpenBlock8)
            .AppendLine(CloseBlock8)
            .AppendLine(CloseBlock4)
            .AppendLine("}");
        return builder.ToString();
    }

    private static StringBuilder CreateHeader(string? namespaceName)
    {
        var builder = new StringBuilder("""
            // <auto-generated />
            #nullable enable

            """);

        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            builder.AppendLine();
        }

        return builder;
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

    private enum UnderlyingKind
    {
        Unsupported,
        String,
        Guid,
        Int32,
        Decimal,
        DateOnly,
    }

    private enum ValueObjectTemplate
    {
        // Values mirror SharedKernel.Domain.ValueObjectTemplate because attribute arguments arrive as
        // integers.
        None = 0,
        ApiVersion = 1,
        NonEmptyString = 2,
        Slug = 3,
        StronglyTypedId = 4,
        IsoCode = 5,
    }

    private sealed class ValueObjectModel
    {
        public string? NamespaceName { get; set; }

        public string? TypeName { get; set; }

        public string? Accessibility { get; set; }

        public string? UnderlyingTypeName { get; set; }

        public UnderlyingKind UnderlyingKind { get; set; }

        public bool Parsing { get; set; }

        public bool Json { get; set; }

        public bool EfCore { get; set; }

        public ValueObjectTemplate Template { get; set; }

        public string? CoreHintName { get; set; }

        public string? JsonHintName { get; set; }

        public string? EfCoreHintName { get; set; }

        public Diagnostic? Diagnostic { get; set; }

        public Location? Location { get; set; }
    }
}
