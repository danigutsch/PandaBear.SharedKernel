using PandaBear.BuildingBlocks;
using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the contact card and the safe schemes of its links.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ContactCardTests : BunitContext
{
    [Fact]
    public void Card_shows_the_address_and_builds_phone_email_and_whatsapp_links()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters
            .Add(component => component.Title, "Contact")
            .Add(component => component.AddressLines, ["Rua A, 1", "Sao Paulo"])
            .Add(component => component.Phone, Phone("+55 (11) 99999-0000"))
            .Add(component => component.Email, Email("Hello@Example.org"))
            .Add(component => component.WhatsApp, Phone("+55 11 99999 0000")));

        // Assert
        cut.Find("section > h3").TextContent.ShouldBe("Contact");
        var address = cut.Find("address");
        address.TextContent.ShouldContain("Rua A, 1", StringComparison.Ordinal);
        address.TextContent.ShouldContain("Sao Paulo", StringComparison.Ordinal);
        var links = address.QuerySelectorAll("a");
        links.Select(link => link.GetAttribute("href")).ShouldBeEquivalentTo(
            "tel:+5511999990000",
            "mailto:hello@example.org",
            "https://wa.me/5511999990000");
        links[0].TextContent.ShouldBe("+5511999990000");
        links[2].TextContent.ShouldBe("WhatsApp");
    }

    [Fact]
    public void The_whatsapp_link_text_can_be_translated()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters
            .Add(component => component.WhatsApp, Phone("5511999990000"))
            .Add(component => component.WhatsAppLabel, "Fale pelo WhatsApp"));

        // Assert
        cut.Find("a").TextContent.ShouldBe("Fale pelo WhatsApp");
        cut.Find("a").GetAttribute("href").ShouldBe("https://wa.me/5511999990000");
    }

    [Fact]
    public void Missing_channels_and_title_render_nothing()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters.Add(component => component.Email, Email("hello@example.org")));

        // Assert
        cut.FindAll("a").Count.ShouldBe(1);
        cut.FindAll("h3").ShouldBeEmpty();
        cut.FindAll(".site-contact-card__lines").ShouldBeEmpty();
    }

    [Fact]
    public void Without_lines_and_channels_no_address_is_rendered()
    {
        // Arrange & Act
        var titleOnly = Render<ContactCard>(parameters => parameters.Add(component => component.Title, "Contact"));
        var emptyLines = Render<ContactCard>(parameters => parameters.Add(component => component.AddressLines, []));

        // Assert
        titleOnly.FindAll("address").ShouldBeEmpty();
        titleOnly.Find("h3").TextContent.ShouldBe("Contact");
        emptyLines.FindAll("address").ShouldBeEmpty();
    }

    [Fact]
    public void Lines_without_channels_render_no_empty_list()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters.Add(component => component.AddressLines, ["Rua A, 1"]));

        // Assert
        cut.Find("address").TextContent.ShouldContain("Rua A, 1", StringComparison.Ordinal);
        cut.FindAll("ul").ShouldBeEmpty();
    }

    [Fact]
    public void Phone_shows_its_display_text_while_the_link_uses_the_digits()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters
            .Add(component => component.Phone, Phone("+55 (11) 99999-0000"))
            .Add(component => component.PhoneText, "+55 (11) 99999-0000"));

        // Assert
        var link = cut.Find("a");
        link.TextContent.ShouldBe("+55 (11) 99999-0000");
        link.GetAttribute("href").ShouldBe("tel:+5511999990000");
    }

    [Theory]
    [InlineData("+55 (11) 99999-0001")]
    [InlineData("call us")]
    public void Phone_text_that_is_not_the_same_number_is_rejected(string text)
    {
        // Arrange & Act
        var render = () => Render<ContactCard>(parameters => parameters
            .Add(component => component.Phone, Phone("+55 (11) 99999-0000"))
            .Add(component => component.PhoneText, text));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ContactCard.PhoneText));
    }

    [Theory]
    [InlineData("a@b.example?cc=evil@example.org")]
    [InlineData("a@b.example?subject=x")]
    [InlineData("a&b@c.example")]
    public void Restored_email_with_characters_unsafe_in_mailto_is_rejected(string stored)
    {
        // Arrange & Act
        var render = () => Render<ContactCard>(parameters => parameters.Add(component => component.Email, EmailAddress.Restore(stored)));

        // Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("12")]
    [InlineData("javascript:alert(1)")]
    public void Restored_phone_that_is_not_digits_is_rejected_for_tel_and_whatsapp(string stored)
    {
        // Arrange & Act
        var phone = () => Render<ContactCard>(parameters => parameters.Add(component => component.Phone, PhoneNumber.Restore(stored)));
        var whatsApp = () => Render<ContactCard>(parameters => parameters.Add(component => component.WhatsApp, PhoneNumber.Restore(stored)));

        // Assert
        phone.ShouldThrow<ArgumentException>();
        whatsApp.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_root()
    {
        // Arrange & Act
        var cut = Render<ContactCard>(parameters => parameters
            .Add(component => component.Email, Email("hello@example.org"))
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var root = cut.Find(".site-contact-card");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        root.GetAttribute("data-test").ShouldBe("x");
    }
}
