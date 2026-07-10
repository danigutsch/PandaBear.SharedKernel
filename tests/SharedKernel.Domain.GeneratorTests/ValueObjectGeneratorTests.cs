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
        generatedSource.ShouldContain("var isValid = value > 0;", StringComparison.Ordinal);
        jsonSource.ShouldContain("writer.WriteStringValue(value.ToRouteSegment());", StringComparison.Ordinal);
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

                [GenerateValueObject(UnderlyingType = typeof(global::System.DateOnly), Parsing = true)]
                public readonly partial record struct DepartureDate;

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
