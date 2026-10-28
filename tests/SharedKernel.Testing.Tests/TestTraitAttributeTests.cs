using Xunit.v3;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the typed trait attributes other than <see cref="TestScopeAttribute" />.</summary>
[TestScope(TestScope.Unit)]
public sealed class TestTraitAttributeTests
{
    /// <summary>Every attribute instance with the trait it must emit.</summary>
    public static TheoryData<ITraitAttribute, string, string> Attributes => new()
    {
        { new TestHostAttribute(TestHost.InProcess), "Host", "in-process" },
        { new TestHostAttribute(TestHost.TestServer), "Host", "test-server" },
        { new TestHostAttribute(TestHost.Aspire), "Host", "aspire" },
        { new TestHostAttribute(TestHost.Browser), "Host", "browser" },
        { new TestFocusAttribute(TestFocus.CoreBehavior), "Focus", "core-behavior" },
        { new TestFocusAttribute(TestFocus.DomainRule), "Focus", "domain-rule" },
        { new TestFocusAttribute(TestFocus.EdgeCase), "Focus", "edge-case" },
        { new TestFocusAttribute(TestFocus.Regression), "Focus", "regression" },
        { new TestFocusAttribute(TestFocus.Security), "Focus", "security" },
        { new TestFocusAttribute(TestFocus.DriftGuard), "Focus", "drift-guard" },
        { new TestFocusAttribute(TestFocus.Migration), "Focus", "migration" },
        { new TestPriorityAttribute(TestPriority.Smoke), "Priority", "smoke" },
        { new TestPriorityAttribute(TestPriority.Critical), "Priority", "critical" },
        { new TestAreaAttribute("catalog"), "Area", "catalog" },
        { new TestSurfaceAttribute("openapi"), "Surface", "openapi" },
    };

    [Fact]
    public static void Every_enum_member_is_covered()
    {
        // Act, Assert
        var expected = Enum.GetValues<TestHost>().Length + Enum.GetValues<TestFocus>().Length + Enum.GetValues<TestPriority>().Length;
        var covered = Attributes.Count(row => row.Data.Item1 is TestHostAttribute or TestFocusAttribute or TestPriorityAttribute);
        covered.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(Attributes))]
    public static void Attribute_emits_its_trait(ITraitAttribute attribute, string name, string value)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(attribute);

        // Act
        var traits = attribute.GetTraits();

        // Assert
        var trait = traits.ShouldHaveSingleItem();
        trait.Key.ShouldBe(name);
        trait.Value.ShouldBe(value);
    }

    [Fact]
    public static void Enum_attributes_reject_undefined_values()
    {
        // Arrange
        var host = () => new TestHostAttribute((TestHost)999);
        var focus = () => new TestFocusAttribute((TestFocus)999);
        var priority = () => new TestPriorityAttribute((TestPriority)999);

        // Act, Assert
        host.ShouldThrow<ArgumentOutOfRangeException>();
        focus.ShouldThrow<ArgumentOutOfRangeException>();
        priority.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public static void String_attributes_reject_blank_values(string value)
    {
        // Arrange
        var area = () => new TestAreaAttribute(value);
        var surface = () => new TestSurfaceAttribute(value);

        // Act, Assert
        area.ShouldThrow<ArgumentException>();
        surface.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public static void Trait_names_match_the_documented_names()
    {
        // Act, Assert
        TestTraitNames.FocusName.ShouldBe("Focus");
        TestTraitNames.PriorityName.ShouldBe("Priority");
    }
}
