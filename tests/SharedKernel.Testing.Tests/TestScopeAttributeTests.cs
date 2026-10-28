namespace PandaBear.Testing.Tests;

/// <summary>Tests that <see cref="TestScopeAttribute" /> emits the same trait as <c>[Trait("Scope", ...)]</c>.</summary>
[TestScope(TestScope.Unit)]
public sealed class TestScopeAttributeTests
{
    /// <summary>Every scope with its documented trait value.</summary>
    public static TheoryData<TestScope, string> ScopeValues => new()
    {
        { TestScope.Unit, "unit" },
        { TestScope.Behavior, "behavior" },
        { TestScope.Component, "component" },
        { TestScope.Contract, "contract" },
        { TestScope.UiIntegration, "ui-integration" },
        { TestScope.Integration, "integration" },
        { TestScope.System, "system" },
        { TestScope.Architecture, "architecture" },
    };

    [Fact]
    public static void Every_scope_has_a_documented_trait_value()
    {
        // Act, Assert
        Enum.GetValues<TestScope>().Length.ShouldBe(ScopeValues.Count);
    }

    [Theory]
    [MemberData(nameof(ScopeValues))]
    public static void Scope_maps_to_its_documented_trait_value(TestScope scope, string expected)
    {
        // Act
        var value = scope.ToTraitValue();

        // Assert
        value.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(ScopeValues))]
    public static void Attribute_emits_the_scope_trait(TestScope scope, string expected)
    {
        // Arrange
        var attribute = new TestScopeAttribute(scope);

        // Act
        var traits = attribute.GetTraits();

        // Assert
        var trait = traits.ShouldHaveSingleItem();
        trait.Key.ShouldBe(TestTraitNames.ScopeName);
        trait.Value.ShouldBe(expected);
    }

    [Fact]
    public static void Attribute_rejects_undefined_scope()
    {
        // Act
        var action = () => new TestScopeAttribute((TestScope)999);

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public static void Attribute_is_allowed_on_assembly_class_and_method()
    {
        // Act
        var usage = typeof(TestScopeAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .ShouldHaveSingleItem();

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method);
    }
}
