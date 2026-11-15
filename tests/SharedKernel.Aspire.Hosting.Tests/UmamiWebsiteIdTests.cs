using Aspire.Hosting;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the name-based (RFC 9562 version 5) website id: the published test vector, the version and variant bits, and its stability.</summary>
public sealed class UmamiWebsiteIdTests
{
    private static readonly Guid DnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void A_name_based_ID_matches_the_published_version_five_test_vector()
    {
        // Arrange
        const string name = "www.example.com";

        // Act
        var id = UmamiWebsiteIds.NameBased(DnsNamespace, name);

        // Assert
        id.ShouldBe(Guid.Parse("2ed6657d-e927-568b-95e1-2665a8aea6a2"), "RFC 9562, Appendix A.4");
        id.Version.ShouldBe(5);
        var variantBits = id.Variant & 0xC;
        variantBits.ShouldBe(0x8, "the variant bits are 10, the variant of RFC 9562");
    }

    [Fact]
    public void A_website_ID_is_the_same_for_the_same_umami_website_and_domain_and_differs_otherwise()
    {
        // Arrange
        var first = UmamiWebsiteIds.Create("umami", "site", "example.com");

        // Act
        var again = UmamiWebsiteIds.Create("Umami", "SITE", "example.com");
        var others = new[]
        {
            UmamiWebsiteIds.Create("analytics", "site", "example.com"),
            UmamiWebsiteIds.Create("umami", "shop", "example.com"),
            UmamiWebsiteIds.Create("umami", "site", "example.org"),
        };

        // Assert
        again.ShouldBe(first, "resource names do not differ by case");
        others.ShouldAllSatisfy(other => other.ShouldNotBe(first));
        first.Version.ShouldBe(5);
    }
}
