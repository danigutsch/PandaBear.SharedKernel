namespace SharedKernel.Domain.Tests;

/// <summary>
/// Tests ValueObject equality by components, type and reference, and its null-safe operators.
/// </summary>
public sealed class ValueObjectComponentTests
{
    [Fact]
    public void Value_objects_are_equal_when_components_match()
    {
        // Arrange
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Lisbon", 3);

        // Act
        var areEqual = first == second;
        var areDifferent = first != second;

        // Assert
        second.ShouldBe(first);
        areEqual.ShouldBeTrue();
        areDifferent.ShouldBeFalse();
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Value_object_equality_handles_reference_and_type_comparisons()
    {
        // Arrange
        var valueObject = new TestValueObject("Lisbon", 3);

        // Act
        var equalsItself = valueObject.Equals(valueObject);
        var equalsOtherType = valueObject.Equals(new OtherTestValueObject("Lisbon", 3));

        // Assert
        equalsItself.ShouldBeTrue();
        equalsOtherType.ShouldBeFalse();
    }

    [Fact]
    public void Value_objects_are_not_equal_when_components_differ()
    {
        // Arrange
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Porto", 3);

        // Act, Assert
        second.ShouldNotBe(first);
    }

    [Fact]
    public void Value_objects_are_not_equal_when_later_components_differ()
    {
        // Arrange
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Lisbon", 4);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        areEqual.ShouldBeFalse();
        second.ShouldNotBe(first);
    }

    [Fact]
    public void Value_object_operators_treat_two_null_values_as_equal()
    {
        // Arrange
        ValueObject?[] operands = [null, null];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        areEqual.ShouldBeTrue();
        areNotEqual.ShouldBeFalse();
    }

    [Fact]
    public void Value_object_operators_treat_left_value_and_right_null_as_not_equal()
    {
        // Arrange
        ValueObject?[] operands = [new TestValueObject("Lisbon", 3), null];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        areEqual.ShouldBeFalse();
        areNotEqual.ShouldBeTrue();
    }

    [Fact]
    public void Value_object_operators_treat_left_null_and_right_value_as_not_equal()
    {
        // Arrange
        ValueObject?[] operands = [null, new TestValueObject("Lisbon", 3)];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        areEqual.ShouldBeFalse();
        areNotEqual.ShouldBeTrue();
    }
}
