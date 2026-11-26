using System.Reflection;
using Microsoft.AspNetCore.Components;
using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Verifies that component parameters leave HTML global attributes available for root forwarding.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteComponentParameterConventionTests
{
    [Fact]
    public void Site_components_keep_HTML_global_attribute_names_available_for_forwarding()
    {
        // Arrange
        // https://html.spec.whatwg.org/multipage/dom.html#global-attributes plus the ARIA role attribute.
        HashSet<string> globalAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "accesskey", "autocapitalize", "autocorrect", "autofocus", "class", "contenteditable",
            "customelementregistry", "dir", "draggable", "enterkeyhint", "exportparts", "headingoffset",
            "headingreset", "hidden", "id", "inert", "inputmode", "is", "itemid", "itemprop", "itemref",
            "itemscope", "itemtype", "lang", "nonce", "part", "popover", "role", "slot", "spellcheck",
            "style", "tabindex", "title", "translate", "writingsuggestions",
        };
        // These parameters supply the actual HTML attribute on the root or on the inline style element.
        HashSet<(Type? Component, string Name)> forwardedAttributes =
        [
            (typeof(SiteComponent), nameof(SiteComponent.Class)),
            (typeof(ThemePreview), nameof(ThemePreview.Nonce)),
        ];
        var components = typeof(SiteComponent).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(SiteComponent)))
            .ToArray();

        // Act
        var collisions = components
            .SelectMany(component => component.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => globalAttributes.Contains(property.Name)
                    && Attribute.IsDefined(property, typeof(ParameterAttribute), inherit: true)
                    && !forwardedAttributes.Contains((property.DeclaringType, property.Name)))
                .Select(property => $"{component.Name}.{property.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        components.ShouldNotBeEmpty();
        collisions.ShouldBeEmpty();
    }
}
