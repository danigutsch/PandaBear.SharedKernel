using SharedKernel.BuildingBlocks;
using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the <c>tel:</c>, <c>mailto:</c> and WhatsApp links built from contact values, and the values they refuse to link.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ContactLinksTests
{
    [Theory]
    [InlineData("+55 (11) 99999-0000", "tel:+5511999990000")]
    [InlineData("11.9999.0000", "tel:1199990000")]
    public void A_phone_number_links_with_tel_and_only_its_digits(string number, string expected)
    {
        // Act
        var link = ContactLinks.Phone(Phone(number));

        // Assert
        link.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("+55 (11) 99999-0000")]
    [InlineData("55 11 99999 0000")]
    public void A_whatsapp_number_links_to_wa_me_with_digits_only(string number)
    {
        // Act
        var link = ContactLinks.WhatsApp(Phone(number));

        // Assert
        link.ShouldNotBeNull().Value.ShouldBe("https://wa.me/5511999990000");
    }

    [Theory]
    [InlineData("Hello@Example.org", "mailto:hello@example.org")]
    [InlineData("first.last+tours_2-b@mail.example.org", "mailto:first.last+tours_2-b@mail.example.org")]
    [InlineData("contato@viajantés.com.br", "mailto:contato@xn--viajants-h1a.com.br")]
    public void An_email_address_links_with_mailto_and_a_punycode_domain(string address, string expected)
    {
        // Act
        var link = ContactLinks.Email(Email(address));

        // Assert
        link.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a@b.example?cc=evil@example.org")]
    [InlineData("a?subject=x@b.example")]
    [InlineData("a&cc=evil@b.example")]
    [InlineData("a%0Abcc=evil@b.example")]
    [InlineData("a,b@c.example")]
    [InlineData("a;b@c.example")]
    [InlineData("a/b@c.example")]
    [InlineData("a#b@c.example")]
    [InlineData("\"a\"@b.example")]
    [InlineData("ação@b.example")]
    [InlineData("a@b.example/x")]
    [InlineData("a@b_c.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("javascript:alert(1)@b.example")]
    [InlineData("@b.example")]
    [InlineData("a@")]
    [InlineData("a@b@c.example")]
    public void An_email_address_that_mailto_cannot_carry_safely_gets_no_link(string stored)
    {
        // Act
        var link = ContactLinks.Email(EmailAddress.Restore(stored));

        // Assert
        link.ShouldBeNull();
    }

    [Fact]
    public void An_email_address_too_long_for_a_link_gets_no_link()
    {
        // Arrange
        var address = EmailAddress.Restore(new string('a', LinkTarget.MaxLength) + "@b.example");

        // Act
        var link = ContactLinks.Email(address);

        // Assert
        link.ShouldBeNull();
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("12")]
    [InlineData("+")]
    [InlineData("1234567890123456")]
    [InlineData("javascript:alert(1)")]
    [InlineData("++5511999990000")]
    public void A_phone_number_that_is_not_a_validated_number_of_digits_gets_no_tel_or_whatsapp_link(string stored)
    {
        // Arrange
        var number = PhoneNumber.Restore(stored);

        // Act
        var links = (ContactLinks.Phone(number), ContactLinks.WhatsApp(number));

        // Assert
        links.ShouldBe((null, null));
    }

    [Fact]
    public void A_missing_value_throws()
    {
        // Act
        var phone = () => ContactLinks.Phone(null!);
        var whatsApp = () => ContactLinks.WhatsApp(null!);
        var email = () => ContactLinks.Email(null!);

        // Assert
        phone.ShouldThrow<ArgumentNullException>();
        whatsApp.ShouldThrow<ArgumentNullException>();
        email.ShouldThrow<ArgumentNullException>();
    }
}
