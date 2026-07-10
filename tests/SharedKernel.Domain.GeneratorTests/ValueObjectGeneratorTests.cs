using Microsoft.CodeAnalysis;

namespace SharedKernel.Domain.GeneratorTests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ValueObjectCapability)]
public sealed class ValueObjectGeneratorTests
{
    [Fact]
    public void Generates_scalar_value_object_creation_and_parsing_for_string_record_struct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Parsing = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourCode.ValueObject.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("public readonly partial record struct TourCode", StringComparison.Ordinal);
        generatedSource.ShouldContain("public string Value { get; }", StringComparison.Ordinal);
        generatedSource.ShouldContain("public static TourCode Create(string value)", StringComparison.Ordinal);
        generatedSource.ShouldContain("public static bool TryCreate(string value, out TourCode result)", StringComparison.Ordinal);
        generatedSource.ShouldContain("public static bool TryParse(string? text, global::System.IFormatProvider? provider, out TourCode result)", StringComparison.Ordinal);
        generatedSource.ShouldContain("static partial void ValidateValue(string value, ref bool isValid);", StringComparison.Ordinal);
        generatedSource.ShouldContain("if (isValid)", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_json_converter_when_json_is_requested()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Json = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourCode.Json.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("public sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<TourCode>", StringComparison.Ordinal);
        generatedSource.ShouldContain("public override TourCode Read(ref global::System.Text.Json.Utf8JsonReader reader", StringComparison.Ordinal);
        generatedSource.ShouldContain("public override void Write(global::System.Text.Json.Utf8JsonWriter writer, TourCode value", StringComparison.Ordinal);
        generatedSource.ShouldContain("reader.TokenType != global::System.Text.Json.JsonTokenType.String", StringComparison.Ordinal);
        generatedSource.ShouldContain("TourCode.TryCreate(value, out var parsedValue)", StringComparison.Ordinal);
        generatedSource.ShouldContain("if (!TourCode.TryCreate(value.Value, out _))", StringComparison.Ordinal);
        generatedSource.ShouldContain("throw new global::System.Text.Json.JsonException", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_json_converter_with_json_exceptions_for_invalid_scalar_values()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(global::System.Guid), Json = true, Template = ValueObjectTemplate.StronglyTypedId)]
            public readonly partial record struct TourId;

            [GenerateValueObject(UnderlyingType = typeof(int), Json = true)]
            public readonly partial record struct TourCount;

            [GenerateValueObject(UnderlyingType = typeof(decimal), Json = true)]
            public readonly partial record struct TourPrice;

            [GenerateValueObject(UnderlyingType = typeof(global::System.DateOnly), Json = true)]
            public readonly partial record struct DepartureDate;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var guidSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourId.Json.g.cs");
        var intSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourCount.Json.g.cs");
        var decimalSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourPrice.Json.g.cs");
        var dateOnlySource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.DepartureDate.Json.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        guidSource.ShouldContain("reader.TokenType == global::System.Text.Json.JsonTokenType.String", StringComparison.Ordinal);
        guidSource.ShouldContain("reader.TryGetGuid(out var value) && TourId.TryCreate(value, out var parsedValue)", StringComparison.Ordinal);
        guidSource.ShouldContain("if (!TourId.TryCreate(value.Value, out _))", StringComparison.Ordinal);
        intSource.ShouldContain("reader.TokenType == global::System.Text.Json.JsonTokenType.Number", StringComparison.Ordinal);
        intSource.ShouldContain("reader.TryGetInt32(out var value) && TourCount.TryCreate(value, out var parsedValue)", StringComparison.Ordinal);
        intSource.ShouldContain("if (!TourCount.TryCreate(value.Value, out _))", StringComparison.Ordinal);
        decimalSource.ShouldContain("reader.TokenType == global::System.Text.Json.JsonTokenType.Number", StringComparison.Ordinal);
        decimalSource.ShouldContain("reader.TryGetDecimal(out var value) && TourPrice.TryCreate(value, out var parsedValue)", StringComparison.Ordinal);
        decimalSource.ShouldContain("if (!TourPrice.TryCreate(value.Value, out _))", StringComparison.Ordinal);
        dateOnlySource.ShouldContain("reader.TokenType != global::System.Text.Json.JsonTokenType.String", StringComparison.Ordinal);
        dateOnlySource.ShouldContain("DepartureDate.TryCreate(parsed, out var parsedValue)", StringComparison.Ordinal);
        dateOnlySource.ShouldContain("if (!DepartureDate.TryCreate(value.Value, out _))", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_ef_core_converter_when_ef_core_is_requested_and_referenced()
    {
        // Arrange
        const string source = """
            namespace Microsoft.EntityFrameworkCore.Storage.ValueConversion
            {
                public sealed class ConverterMappingHints
                {
                }

                public class ValueConverter<TModel, TProvider>
                {
                    protected ValueConverter(
                        global::System.Linq.Expressions.Expression<global::System.Func<TModel, TProvider>> convertToProviderExpression,
                        global::System.Linq.Expressions.Expression<global::System.Func<TProvider, TModel>> convertFromProviderExpression,
                        ConverterMappingHints? mappingHints = null)
                    {
                    }
                }
            }

            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), EfCore = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourCode.EfCore.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("public sealed class EfCoreValueConverter", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TourCode, string>", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_diagnostic_when_ef_core_is_requested_without_reference()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), EfCore = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL017");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generates_api_version_template_route_segment_and_comparison()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(int), Parsing = true, Json = true, Template = ValueObjectTemplate.ApiVersion)]
            public readonly partial record struct ContractVersion;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.ContractVersion.ValueObject.g.cs");
        var jsonSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.ContractVersion.Json.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("public readonly partial record struct ContractVersion : global::System.IComparable<ContractVersion>", StringComparison.Ordinal);
        generatedSource.ShouldContain("public readonly string ToRouteSegment()", StringComparison.Ordinal);
        generatedSource.ShouldContain("return \"v\" + Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);", StringComparison.Ordinal);
        generatedSource.ShouldContain("var normalized = global::System.MemoryExtensions.AsSpan(text);", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::System.MemoryExtensions.StartsWith", StringComparison.Ordinal);
        generatedSource.ShouldContain("normalized = normalized.Slice(1);", StringComparison.Ordinal);
        generatedSource.ShouldContain("var isValid = value > 0;", StringComparison.Ordinal);
        generatedSource.Contains("Substring(1)", StringComparison.Ordinal).ShouldBe(false);
        jsonSource.ShouldContain("reader.TryGetInt32(out var value) && ContractVersion.TryCreate(value, out var parsedNumber)", StringComparison.Ordinal);
        jsonSource.ShouldContain("reader.TokenType != global::System.Text.Json.JsonTokenType.String", StringComparison.Ordinal);
        jsonSource.ShouldContain("throw new global::System.Text.Json.JsonException", StringComparison.Ordinal);
        jsonSource.ShouldContain("writer.WriteStringValue(value.ToRouteSegment());", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_invariant_default_for_formatted_scalar_parsing()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(int), Parsing = true)]
            public readonly partial record struct TourCount;

            [GenerateValueObject(UnderlyingType = typeof(decimal), Parsing = true)]
            public readonly partial record struct TourPrice;

            [GenerateValueObject(UnderlyingType = typeof(global::System.DateOnly), Parsing = true)]
            public readonly partial record struct DepartureDate;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var intSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourCount.ValueObject.g.cs");
        var decimalSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourPrice.ValueObject.g.cs");
        var dateOnlySource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.DepartureDate.ValueObject.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        intSource.ShouldContain("provider ?? global::System.Globalization.CultureInfo.InvariantCulture", StringComparison.Ordinal);
        decimalSource.ShouldContain("provider ?? global::System.Globalization.CultureInfo.InvariantCulture", StringComparison.Ordinal);
        dateOnlySource.ShouldContain("provider ?? global::System.Globalization.CultureInfo.InvariantCulture", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_diagnostic_when_underlying_type_is_missing()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL010");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_is_not_readonly_record_struct()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL011");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_is_not_partial()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL012");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_is_nested()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed partial class Container
            {
                [GenerateValueObject(UnderlyingType = typeof(string))]
                public readonly partial record struct TourCode;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL013");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_is_generic()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct TourCode<T>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL014");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_underlying_type_is_not_supported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(global::System.DateTimeOffset))]
            public readonly partial record struct TourInstant;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL015");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_template_does_not_match_underlying_type()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.ApiVersion)]
            public readonly partial record struct TourVersion;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL018");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_declares_constructor()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct TourCode
            {
                public TourCode(string value)
                {
                    Value = value;
                }

                public string Value { get; }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL019");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_value_object_declares_generated_member_name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct TourCode
            {
                public string Value => "existing";
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL020");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("Value", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_distinct_sources_for_same_type_name_in_different_namespaces()
    {
        // Arrange
        const string source = """
            namespace First
            {
                [GenerateValueObject(UnderlyingType = typeof(string))]
                public readonly partial record struct TourCode;
            }

            namespace Second
            {
                [GenerateValueObject(UnderlyingType = typeof(string))]
                public readonly partial record struct TourCode;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSources = runResult.Results.Single().GeneratedSources;

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSources.ShouldContain(source => string.Equals(source.HintName, "First.TourCode.ValueObject.g.cs", StringComparison.Ordinal));
        generatedSources.ShouldContain(source => string.Equals(source.HintName, "Second.TourCode.ValueObject.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generates_distinct_sources_for_underscored_type_names()
    {
        // Arrange
        const string source = """
            namespace Demo
            {
                [GenerateValueObject(UnderlyingType = typeof(string))]
                public readonly partial record struct Tour_Code;
            }

            namespace Demo.Tour
            {
                [GenerateValueObject(UnderlyingType = typeof(string))]
                public readonly partial record struct Code;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSources = runResult.Results.Single().GeneratedSources;

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSources.ShouldContain(source => string.Equals(source.HintName, "Demo.Tour_Code.ValueObject.g.cs", StringComparison.Ordinal));
        generatedSources.ShouldContain(source => string.Equals(source.HintName, "Demo.Tour.Code.ValueObject.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generates_once_when_same_partial_type_has_duplicate_value_object_attributes()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct TourCode;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var generatedSources = runResult.Results.Single().GeneratedSources;

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSources.ShouldHaveSingleItem();
    }

    [Fact]
    public void Reports_diagnostic_when_same_partial_type_has_conflicting_value_object_attributes()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Parsing = true)]
            public readonly partial record struct TourCode;

            [GenerateValueObject(UnderlyingType = typeof(string), Json = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL021");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Reports_diagnostic_when_same_declaration_has_conflicting_value_object_attributes()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Parsing = true)]
            [GenerateValueObject(UnderlyingType = typeof(string), Json = true)]
            public readonly partial record struct TourCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics.ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe("SKMDL021");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generates_template_validation_for_reusable_technical_invariants()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.NonEmptyString)]
            public readonly partial record struct TourName;

            [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.Slug)]
            public readonly partial record struct TourSlug;

            [GenerateValueObject(UnderlyingType = typeof(global::System.Guid), Template = ValueObjectTemplate.StronglyTypedId)]
            public readonly partial record struct TourId;

            [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.IsoCode)]
            public readonly partial record struct CountryCode;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunValueObjectGeneratorDriver(compilation);
        var nonEmptySource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourName.ValueObject.g.cs");
        var slugSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourSlug.ValueObject.g.cs");
        var idSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.TourId.ValueObject.g.cs");
        var isoCodeSource = GeneratorTestHarness.GetGeneratedSource(runResult, "Demo.CountryCode.ValueObject.g.cs");

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        nonEmptySource.ShouldContain("var isValid = !string.IsNullOrWhiteSpace(value);", StringComparison.Ordinal);
        slugSource.ShouldContain("private static bool IsSlug(string value)", StringComparison.Ordinal);
        idSource.ShouldContain("var isValid = value != global::System.Guid.Empty;", StringComparison.Ordinal);
        isoCodeSource.ShouldContain("private static bool IsIsoCode(string value)", StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_value_object_support_compiles()
    {
        // Arrange
        const string source = """
            namespace Microsoft.EntityFrameworkCore.Storage.ValueConversion
            {
                public sealed class ConverterMappingHints
                {
                }

                public class ValueConverter<TModel, TProvider>
                {
                    protected ValueConverter(
                        global::System.Linq.Expressions.Expression<global::System.Func<TModel, TProvider>> convertToProviderExpression,
                        global::System.Linq.Expressions.Expression<global::System.Func<TProvider, TModel>> convertFromProviderExpression,
                        ConverterMappingHints? mappingHints = null)
                    {
                    }
                }
            }

            namespace Demo
            {
                [GenerateValueObject(UnderlyingType = typeof(string), Parsing = true)]
                public readonly partial record struct TourCode;

                [GenerateValueObject(UnderlyingType = typeof(global::System.Guid), Parsing = true)]
                public readonly partial record struct TourId;

                [GenerateValueObject(UnderlyingType = typeof(decimal), Parsing = true)]
                public readonly partial record struct Price;

                [GenerateValueObject(UnderlyingType = typeof(global::System.DateOnly), Parsing = true, Json = true)]
                public readonly partial record struct DepartureDate;

                [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.NonEmptyString)]
                public readonly partial record struct TourName;

                [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.Slug)]
                public readonly partial record struct TourSlug;

                [GenerateValueObject(UnderlyingType = typeof(global::System.Guid), Template = ValueObjectTemplate.StronglyTypedId)]
                public readonly partial record struct StrongTourId;

                [GenerateValueObject(UnderlyingType = typeof(int), Template = ValueObjectTemplate.StronglyTypedId)]
                public readonly partial record struct NumericTourId;

                [GenerateValueObject(UnderlyingType = typeof(string), Template = ValueObjectTemplate.IsoCode)]
                public readonly partial record struct CountryCode;

                [GenerateValueObject(UnderlyingType = typeof(int), Parsing = true, Json = true, EfCore = true, Template = ValueObjectTemplate.ApiVersion)]
                public readonly partial record struct ContractVersion;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var outputCompilation = GeneratorTestHarness.RunValueObjectGeneratorAndUpdateCompilation(compilation, out var runResult);
        var errors = outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }
}
