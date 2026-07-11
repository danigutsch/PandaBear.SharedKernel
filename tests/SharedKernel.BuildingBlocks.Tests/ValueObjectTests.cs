using static SharedKernel.BuildingBlocks.Tests.ValueObjectTestsHelpers;

namespace SharedKernel.BuildingBlocks.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_returns_true_for_equal_values()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        var result = address1.Equals(address2);

        (result).ShouldBeTrue();
    }

    [Fact]
    public void Equals_returns_false_for_different_values()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        var result = address1.Equals(address2);

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Equals_returns_false_when_other_is_null()
    {
        var address = new TestAddress("123 Main St", "Springfield");

        var result = EqualsObject(address, null);

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Equals_returns_false_for_different_types()
    {
        var address = new TestAddress("123 Main St", "Springfield");
        var money = new TestMoney(100m, "USD");

        var result = address.Equals(money);

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Equals_object_returns_false_for_non_value_object()
    {
        var address = new TestAddress("123 Main St", "Springfield");
        object other = "Not a value object";

        var result = address.Equals(other);

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Get_hash_code_returns_same_value_for_equal_objects()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        (hash2).ShouldBe(hash1);
    }

    [Fact]
    public void Get_hash_code_returns_different_values_for_different_objects()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        (hash2).ShouldNotBe(hash1);
    }

    [Fact]
    public void Get_hash_code_handles_null_components()
    {
        var address = new TestAddress("123 Main St", null);

        var hash = address.GetHashCode();

        (hash).ShouldNotBe(0);
    }

    [Fact]
    public void Operator_equals_returns_false_for_different_values()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        var result = address1 == address2;

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Operator_not_equals_returns_false_for_equal_values()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        var result = address1 != address2;

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Operator_not_equals_returns_true_for_different_values()
    {
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("456 Oak Ave", "Springfield");

        var result = address1 != address2;

        (result).ShouldBeTrue();
    }

    [Fact]
    public void Value_object_comparison_respects_component_order()
    {
        var value1 = new TestTwoStrings("A", "B");
        var value2 = new TestTwoStrings("B", "A");

        var result = value1.Equals(value2);

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Value_object_with_multiple_components_compares_all()
    {
        var person1 = new TestPerson("John", "Doe", 30);
        var person2 = new TestPerson("John", "Doe", 30);
        var person3 = new TestPerson("John", "Doe", 31);

        (person1 == person2).ShouldBeTrue();
        (person1 == person3).ShouldBeFalse();
    }

    [Fact]
    public void Value_object_with_null_components_compares_correctly()
    {
        var address1 = new TestAddress("123 Main St", null);
        var address2 = new TestAddress("123 Main St", null);
        var address3 = new TestAddress("123 Main St", "Springfield");

        (address1 == address2).ShouldBeTrue();
        (address1 == address3).ShouldBeFalse();
    }

    [Fact]
    public void Value_object_dictionary_lookup_works_with_equal_instance()
    {
        var dictionary = new Dictionary<TestAddress, string>();
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        dictionary[address1] = "Home";

        (dictionary[address2]).ShouldBe("Home");
    }

    [Fact]
    public void Value_object_hash_set_contains_works_with_equal_instance()
    {
        var hashSet = new HashSet<TestAddress>();
        var address1 = new TestAddress("123 Main St", "Springfield");
        var address2 = new TestAddress("123 Main St", "Springfield");

        hashSet.Add(address1);

        (hashSet).ShouldContain(address2);
    }

    [Fact]
    public void Value_object_with_complex_types_compares_correctly()
    {
        var order1 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("123 Main St", "Springfield"));
        var order2 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("123 Main St", "Springfield"));
        var order3 = new TestOrder(new TestMoney(100m, "USD"), new TestAddress("456 Oak Ave", "Springfield"));

        (order1 == order2).ShouldBeTrue();
        (order1 == order3).ShouldBeFalse();
    }

}
