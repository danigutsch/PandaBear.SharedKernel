using System.Reflection;
using PandaBear.Testing.Playwright;

namespace PandaBear.Blazor.Tests;

/// <summary>Keeps the Playwright test-support package decoupled from this library while its selector copy cannot drift.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightHelperContractTests
{
    [Fact]
    public void The_playwright_ready_selector_equals_the_marker_selector()
    {
        // Act
        var selector = PlaywrightPageExtensions.InteractiveReadySelector;

        // Assert
        selector.ShouldBe(InteractiveMarker.ReadySelector);
        selector.ShouldBe($"[{InteractiveMarker.AttributeName}='true']");
    }

    [Fact]
    public void The_playwright_package_does_not_reference_the_blazor_library()
    {
        // Act
        var referenced = typeof(PlaywrightPageExtensions).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

        // Assert
        referenced.ShouldNotContain(typeof(InteractiveMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void The_kestrel_page_test_names_its_environment_member_without_hiding_system_environment()
    {
        // Arrange
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Act
        var environmentName = typeof(KestrelPageTest<>).GetProperty("EnvironmentName", members);
        var hidingProperty = typeof(KestrelPageTest<>).GetProperty("Environment", members);

        // Assert
        environmentName.ShouldNotBeNull();
        environmentName.GetMethod!.IsVirtual.ShouldBeTrue();
        hidingProperty.ShouldBeNull();
    }
}
