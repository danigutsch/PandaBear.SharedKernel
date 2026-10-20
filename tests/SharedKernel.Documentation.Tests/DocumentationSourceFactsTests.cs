using static PandaBear.Documentation.DocumentationSourceFacts;

namespace PandaBear.Documentation.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class DocumentationSourceFactsTests
{
    [Fact]
    public void Event_extraction_should_ignore_unrelated_is_patterns_and_support_switch_pattern_forms()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static int Apply(object value)
                {
                    if (value is bool flag)
                    {
                        return flag ? 5 : 6;
                    }

                    switch (value)
                    {
                        case null:
                            return 0;
                        case string text:
                            return text.Length;
                        case int:
                            return 1;
                        case SampleEvent { }:
                            return 2;
                    }

                    return value switch
                    {
                        long => 3,
                        AnotherEvent { } => 4,
                        _ => 0
                    };
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var eventTypes = ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Assert
        eventTypes.ShouldBe(["AnotherEvent", "SampleEvent", "int", "long", "string"]);
    }

    [Fact]
    public void Event_extraction_should_support_parenthesized_binary_patterns()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static int Apply(object value) => value switch
                {
                    (FirstEvent { } or SecondEvent { }) => 1,
                    _ => 0
                };
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var eventTypes = ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Assert
        eventTypes.ShouldBe(["FirstEvent", "SecondEvent"]);
    }

    [Fact]
    public void Event_extraction_should_fail_closed_for_unsupported_patterns()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static bool Apply(int value) => value switch
                {
                    > 0 => true,
                    _ => false
                };
            }
            """;
        using var document = new TemporaryTextDocument(source);
        Action act = () => ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("pattern", StringComparison.OrdinalIgnoreCase);
        exception.Message.ShouldContain(document.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Event_extraction_should_fail_closed_for_typeof_dispatch()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static int Apply(object value)
                {
                    if (value.GetType() == typeof(NewEvent))
                    {
                        return 1;
                    }

                    return value switch
                    {
                        string text => text.Length,
                        _ => 0
                    };
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);
        Action act = () => ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("typeof", StringComparison.OrdinalIgnoreCase);
        exception.Message.ShouldContain(document.Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- doc-fact:sample:start -->\n- item: `First`\n<!-- doc-fact:sample:end -->\n<!-- doc-fact:sample:start -->\n- item: `Second`\n<!-- doc-fact:sample:end -->")]
    [InlineData("<!-- doc-fact:sample:start -->\n<!-- doc-fact:sample:start -->\n- item: `First`\n<!-- doc-fact:sample:end -->\n<!-- doc-fact:sample:end -->")]
    [InlineData("<!-- doc-fact:sample:start -->\n- item: `First`\n- item: `Missing end\n<!-- doc-fact:sample:end -->")]
    [InlineData("<!-- doc-fact:sample:start -->\n<!-- doc-fact:sample:end -->")]
    [InlineData("<!-- doc-fact:sample:start -->\n- item: `First`\n- item: `First`\n<!-- doc-fact:sample:end -->")]
    public void Fact_extraction_should_report_the_document_path_for_malformed_or_duplicate_sections(string content)
    {
        // Arrange
        using var document = new TemporaryTextDocument(content);
        Action act = () => ReadMarkedFactIdentifiers(document.Path, document.Path, "sample", "item");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("sample", StringComparison.Ordinal);
        exception.Message.ShouldContain(document.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Invocation_extraction_should_scope_a_method_and_preserve_duplicates()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static void Active(object services)
                {
                    services.AddProducer();
                    services.AddProducer();
                }

                private static void Unused(object services)
                {
                    services.AddConsumer();
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var identifiers = ReadMethodInvocationIdentifiers(document.Path, "Active", parameterCount: 1);

        // Assert
        identifiers.ShouldBe(["AddProducer", "AddProducer"]);
    }

    [Fact]
    public void Invocation_argument_extraction_should_scope_the_selected_overload_and_call()
    {
        // Arrange
        const string source = """
            internal static class Sample
            {
                public static object AddInfrastructure(this object builder) =>
                    builder.AddInfrastructure(addRuntimeBackgroundServices: null);

                public static object AddInfrastructure(this object builder, bool? addRuntimeBackgroundServices) =>
                    builder;
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var arguments = ReadMethodInvocationArguments(
            document.Path,
            "AddInfrastructure",
            parameterCount: 1,
            invokedMethodName: "AddInfrastructure");

        // Assert
        arguments.ShouldBe(["addRuntimeBackgroundServices: null"]);
    }

    [Fact]
    public void Invocation_argument_extraction_should_normalize_semantically_equivalent_wrapping()
    {
        // Arrange
        const string oneLineSource = """"
            internal static class Sample
            {
                private static void Configure(ref int count) => Track(count: ref count, transform: value => value.Trim(), text: "value  with  spaces", raw: """raw  value""");
            }
            """";
        const string wrappedSource = """"
            internal static class Sample
            {
                private static void Configure(ref int count) => Track(
                    count:
                        ref
                            count,
                    transform:
                        value =>
                            value.Trim(),
                    text:
                        "value  with  spaces",
                    raw:
                        """raw  value""");
            }
            """";
        using var oneLineDocument = new TemporaryTextDocument(oneLineSource);
        using var wrappedDocument = new TemporaryTextDocument(wrappedSource);

        // Act
        var oneLineArguments = ReadMethodInvocationArguments(
            oneLineDocument.Path,
            "Configure",
            parameterCount: 1,
            invokedMethodName: "Track");
        var wrappedArguments = ReadMethodInvocationArguments(
            wrappedDocument.Path,
            "Configure",
            parameterCount: 1,
            invokedMethodName: "Track");

        // Assert
        oneLineArguments.ShouldBe(
        [
            "count: ref count",
            "transform: value => value.Trim()",
            "text: \"value  with  spaces\"",
            "raw: \"\"\"raw  value\"\"\""
        ]);
        wrappedArguments.ShouldBe(oneLineArguments);
    }

    [Fact]
    public void Invocation_extraction_should_support_direct_and_generic_calls()
    {
        // Arrange
        const string source = """
            internal static class Sample
            {
                private static void Active()
                {
                    Configure(value: 1);
                    Configure<int>(value: 2);
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var identifiers = ReadMethodInvocationIdentifiers(document.Path, "Active", parameterCount: 0);
        var arguments = ReadMethodInvocationArguments(
            document.Path,
            "Active",
            parameterCount: 0,
            invokedMethodName: "Configure");

        // Assert
        identifiers.ShouldBe(["Configure", "Configure", "int"]);
        arguments.ShouldBe(["value: 1", "value: 2"]);
    }

    [Fact]
    public void Registration_extraction_should_include_new_hosted_services_without_name_filters()
    {
        // Arrange
        const string source = """
            internal static class Sample
            {
                private static void Configure(object services)
                {
                    services.AddHostedService<AdminCatalogDeliveryPump>();
                    services.AddKnownFlow();
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var identifiers = ReadMethodRegistrationIdentifiers(document.Path, "Configure", parameterCount: 1);

        // Assert
        identifiers.ShouldBe(["AddKnownFlow", "AdminCatalogDeliveryPump"]);
    }

    [Fact]
    public void Registration_extraction_should_support_direct_generic_and_identifier_calls()
    {
        // Arrange
        const string source = """
            internal static class Sample
            {
                private static void Configure()
                {
                    AddHostedService<IntegrationEventWorker>();
                    AddKnownFlow();
                }
            }
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var identifiers = ReadMethodRegistrationIdentifiers(document.Path, "Configure", parameterCount: 0);

        // Assert
        identifiers.ShouldBe(["AddKnownFlow", "IntegrationEventWorker"]);
    }

    [Fact]
    public void Source_extraction_should_report_parse_errors_with_the_source_path()
    {
        // Arrange
        using var document = new TemporaryTextDocument("internal sealed class {");
        Action act = () => ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("Could not parse source", StringComparison.Ordinal);
        exception.Message.ShouldContain(document.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_extraction_should_report_ambiguous_methods_with_the_source_path()
    {
        // Arrange
        const string source = """
            internal sealed class Sample
            {
                private static int Apply(object value) => 0;

                private static int Apply(string value) => 1;
            }
            """;
        using var document = new TemporaryTextDocument(source);
        Action act = () => ReadSwitchCaseTypeNames(document.Path, "Apply", parameterCount: 1);

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("found 2", StringComparison.Ordinal);
        exception.Message.ShouldContain(document.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Top_level_extraction_should_read_invocations_and_hosted_service_registrations()
    {
        // Arrange
        const string source = """
            builder.AddInfrastructure();
            builder.Services.AddHostedService<IntegrationEventWorker>();
            """;
        using var document = new TemporaryTextDocument(source);

        // Act
        var invocations = ReadTopLevelInvocationIdentifiers(document.Path);
        var registrations = ReadTopLevelRegistrationIdentifiers(document.Path);

        // Assert
        invocations.ShouldBe(["AddHostedService", "AddInfrastructure", "IntegrationEventWorker"]);
        registrations.ShouldBe(["AddInfrastructure", "IntegrationEventWorker"]);
    }
}
