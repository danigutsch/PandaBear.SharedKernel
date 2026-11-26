namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Verifies the display and record semantics of a derived redacted record.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "RedactedDisplayRecord")]
public sealed class RedactedDisplayRecordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("synthetic-nested-input")]
    public void Display_uses_the_concrete_name_without_evaluating_payload_members(string? nestedValue)
    {
        // Arrange
        var nested = nestedValue is null ? null : new RedactedDisplayInput(nestedValue);
        var record = new RedactedDisplayProbe(nested) { AddedMember = "synthetic-added-value" };
        var expected = $"{nameof(RedactedDisplayProbe)} [redacted]";

        // Act
        var display = record.ToString();
        var interpolation = $"Command: {record}";

        // Assert
        display.ShouldBe(expected, userMessage: "Display should contain the derived type name and redaction marker alone.");
        interpolation.ShouldBe($"Command: {expected}", userMessage: "Interpolation should use the same redacted record display.");
    }

    [Fact]
    public void Copy_and_deconstruction_preserve_record_value_semantics()
    {
        // Arrange
        var nested = new RedactedDisplayInput("synthetic-nested-input");
        var original = new RedactedDisplayProbe(nested) { AddedMember = "synthetic-original-value" };

        // Act
        var copy = original with { };
        var revised = original with { AddedMember = "synthetic-revised-value" };
        var revisionInput = revised.Input;
        revised.Deconstruct(out var deconstructedInput);
        var equalCopy = original == copy;
        var differentRevision = original != revised;
        var originalHash = original.GetHashCode();
        var copyHash = copy.GetHashCode();

        // Assert
        copy.ShouldNotBeSameAs(original, "An unchanged with-expression should still copy the record.");
        equalCopy.ShouldBeTrue("Copied records with the same values should remain equal.");
        differentRevision.ShouldBeTrue("The derived member should still participate in record equality.");
        copyHash.ShouldBe(originalHash, "Equal record values should produce equal hash codes.");
        revisionInput.ShouldBeSameAs(nested, "Changing an unrelated member should preserve the nested reference.");
        deconstructedInput.ShouldBeSameAs(nested, "Positional deconstruction should return the original nested reference.");
    }
}
