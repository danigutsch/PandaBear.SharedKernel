using static PandaBear.BuildingBlocks.Tests.ValueObjectTestsHelpers;

namespace PandaBear.BuildingBlocks.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_returns_true_for_equal_values()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_returns_false_for_different_values()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_returns_false_when_other_is_null()
    {
        // Arrange
        var address = new TestAddress("123 Main St", "Springfield");

        // Act
        var result = EqualsObject(address, null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_returns_false_for_different_types()
    {
        // Arrange
        var address = new TestAddress("123 Main St", "Springfield");
        var money = new TestMoney(100m, "USD");

        // Act
        var result = address.Equals(money);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_object_returns_false_for_non_value_object()
    {
        // Arrange
        var address = new TestAddress("123 Main St", "Springfield");
        object other = "Not a value object";

        // Act
        var result = address.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Get_hash_code_returns_same_value_for_equal_objects()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        // Act
        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        // Assert
        hash2.ShouldBe(hash1);
    }

    [Fact]
    public void Get_hash_code_returns_different_values_for_different_objects()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        // Act
        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        // Assert
        hash2.ShouldNotBe(hash1);
    }

    [Fact]
    public void Get_hash_code_handles_null_components()
    {
        // Arrange
        var address = new TestAddress("123 Main St", null);

        // Act
        var hash = address.GetHashCode();

        // Assert
        hash.ShouldNotBe(0);
    }

    [Fact]
    public void Operator_equals_returns_false_for_different_values()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        // Act
        var result = address1 == address2;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Operator_not_equals_returns_false_for_equal_values()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        // Act
        var result = address1 != address2;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Operator_not_equals_returns_true_for_different_values()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        // Act
        var result = address1 != address2;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Value_object_comparison_respects_component_order()
    {
        // Arrange
        var value1 = new TestTwoStrings("A", "B");
        var value2 = new TestTwoStrings("B", "A");

        // Act
        var result = value1.Equals(value2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Value_object_with_multiple_components_compares_all()
    {
        // Arrange
        var person1 = new TestPerson("John", "Doe", 30);
        var person2 = new TestPerson("John", "Doe", 30);
        var person3 = new TestPerson("John", "Doe", 31);

        // Act
        var areEqual = person1 == person2;
        var areEqual2 = person1 == person3;

        // Assert
        areEqual.ShouldBeTrue();
        areEqual2.ShouldBeFalse();
    }

    [Fact]
    public void Value_object_with_null_components_compares_correctly()
    {
        // Arrange
        var address1 = new TestAddress("123 Main St", null);
        var address2 = new TestAddress("123 Main St", null);
        var address3 = new TestAddress("123 Main St", "Springfield");

        // Act
        var areEqual = address1 == address2;
        var areEqual2 = address1 == address3;

        // Assert
        areEqual.ShouldBeTrue();
        areEqual2.ShouldBeFalse();
    }

    [Fact]
    public void Value_object_dictionary_lookup_works_with_equal_instance()
    {
        // Arrange
        var dictionary = new Dictionary<TestAddress, string>();
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        // Act
        dictionary[address1] = "Home";

        // Assert
        dictionary[address2].ShouldBe("Home");
    }

    [Fact]
    public void Value_object_hash_set_contains_works_with_equal_instance()
    {
        // Arrange
        var hashSet = new HashSet<TestAddress>();
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        // Act
        hashSet.Add(address1);

        // Assert
        hashSet.ShouldContain(address2);
    }

    [Fact]
    public void Value_object_with_complex_types_compares_correctly()
    {
        // Arrange
        var order1 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("123 Main St", "Springfield"));
        var order2 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("123 Main St", "Springfield"));
        var order3 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("456 Oak Ave", "Springfield"));

        // Act
        var areEqual = order1 == order2;
        var areEqual2 = order1 == order3;

        // Assert
        areEqual.ShouldBeTrue();
        areEqual2.ShouldBeFalse();
    }

}
