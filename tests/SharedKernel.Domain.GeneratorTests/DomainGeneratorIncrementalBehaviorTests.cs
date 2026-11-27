using Microsoft.CodeAnalysis;
using SharedKernel.Domain.SourceGenerator;

namespace SharedKernel.Domain.GeneratorTests;

/// <summary>Tests source reuse when one declaration changes between runs of the same generator driver.</summary>
public sealed class DomainGeneratorIncrementalBehaviorTests
{
    [Fact]
    [Trait(Testing.TestTraitNames.CapabilityName, TestTraits.IdentityCapability)]
    public void An_identity_edit_preserves_the_other_model_output()
    {
        // Arrange
        const string changingSource = """
            namespace Demo;

            [GenerateModelSupport(Identity = true)]
            public sealed partial class ChangingModel : IIdentified<int>
            {
                public int Id { get; private init; }
            }
            """;
        const string stableSource = """
            namespace Demo;

            [GenerateModelSupport(Identity = true)]
            public sealed partial class StableModel : IIdentified<int>
            {
                public int Id { get; private init; }
            }
            """;
        var editedSource = changingSource.Replace("int", "global::System.Guid", StringComparison.Ordinal);
        var generator = new ModelIdentityGenerator();

        // Act
        var (before, after) = DomainGeneratorIncrementalBehaviorTestSupport.RunAcrossEdit(generator, changingSource, editedSource, stableSource);

        // Assert
        DomainGeneratorIncrementalBehaviorTestSupport.AssertIndependentOutputs(
            before, after, "Demo.ChangingModel.ModelSupport.g.cs", "Demo.StableModel.ModelSupport.g.cs");
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CapabilityName, TestTraits.ValueObjectCapability)]
    public void A_value_object_edit_preserves_the_other_model_output()
    {
        // Arrange
        const string changingSource = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(int))]
            public readonly partial record struct ChangingCode;
            """;
        const string stableSource = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct StableCode;
            """;
        var editedSource = changingSource.Replace("typeof(int)", "typeof(global::System.Guid)", StringComparison.Ordinal);
        var generator = new ValueObjectGenerator();

        // Act
        var (before, after) = DomainGeneratorIncrementalBehaviorTestSupport.RunAcrossEdit(generator, changingSource, editedSource, stableSource);

        // Assert
        DomainGeneratorIncrementalBehaviorTestSupport.AssertIndependentOutputs(
            before, after, "Demo.ChangingCode.ValueObject.g.cs", "Demo.StableCode.ValueObject.g.cs");
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CapabilityName, TestTraits.ValueObjectCapability)]
    public void A_value_object_comment_edit_reuses_identical_source_output()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(int))]
            public readonly partial record struct ChangingCode;
            """;
        var editedSource = source.Replace("public readonly", "// A comment about the model.\npublic readonly", StringComparison.Ordinal);
        var generator = new ValueObjectGenerator();

        // Act
        var (before, after) = DomainGeneratorIncrementalBehaviorTestSupport.RunAcrossEdit(generator, source, editedSource);

        // Assert
        const string hintName = "Demo.ChangingCode.ValueObject.g.cs";
        before.Diagnostics.ShouldBeEmpty("The original declaration is supported.");
        after.Diagnostics.ShouldBeEmpty("A comment does not change which declarations are supported.");
        GeneratorTestHarness.GetGeneratedSource(after, hintName)
            .ShouldBe(GeneratorTestHarness.GetGeneratedSource(before, hintName), userMessage: "The comment leaves the generated text identical.");
        var reasons = DomainGeneratorIncrementalBehaviorTestSupport.SourceOutputReasons(after);
        reasons.ShouldNotBeEmpty("The repeated driver must record source output reuse.");
        reasons.ShouldAllSatisfy(static reason => reason.ShouldBeOneOf(
            [IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged],
            "The generator can reuse identical output after a comment edit."));
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CapabilityName, TestTraits.ValueObjectCapability)]
    public void A_value_object_edit_removes_and_restores_output_when_validity_changes()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(int))]
            public readonly partial record struct ChangingCode;
            """;
        const string stableSource = """
            namespace Demo;

            [GenerateValueObject(UnderlyingType = typeof(string))]
            public readonly partial record struct StableCode;
            """;
        var invalidSource = source.Replace("readonly partial record", "readonly record", StringComparison.Ordinal);
        var generator = new ValueObjectGenerator();

        // Act
        var runs = DomainGeneratorIncrementalBehaviorTestSupport.RunAcrossEdits(generator, source, [invalidSource, source], stableSource);
        var (before, invalid, restored) = (runs[0], runs[1], runs[2]);

        // Assert
        const string changingHint = "Demo.ChangingCode.ValueObject.g.cs";
        const string stableHint = "Demo.StableCode.ValueObject.g.cs";
        before.Diagnostics.ShouldBeEmpty("The original declaration is supported.");
        invalid.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKMDL012", userMessage: "Removing partial makes the declaration invalid.");
        invalid.Results.ShouldHaveSingleItem().GeneratedSources.ShouldHaveSingleItem().HintName
            .ShouldBe(stableHint, userMessage: "The invalid declaration stops emitting source while the untouched peer remains.");
        restored.Diagnostics.ShouldBeEmpty("Restoring partial clears the diagnostic.");
        GeneratorTestHarness.GetGeneratedSource(restored, changingHint).ShouldBe(
            GeneratorTestHarness.GetGeneratedSource(before, changingHint), userMessage: "Restoring the declaration restores its generated source.");
        var stableOutput = GeneratorTestHarness.GetGeneratedSource(before, stableHint);
        GeneratorTestHarness.GetGeneratedSource(invalid, stableHint)
            .ShouldBe(stableOutput, userMessage: "The peer's generated source survives the invalid edit.");
        GeneratorTestHarness.GetGeneratedSource(restored, stableHint)
            .ShouldBe(stableOutput, userMessage: "The peer's generated source survives the repair.");
    }
}
