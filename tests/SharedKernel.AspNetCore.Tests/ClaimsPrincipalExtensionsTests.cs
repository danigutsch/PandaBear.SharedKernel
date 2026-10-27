using System.Security.Claims;
using PandaBear.Testing;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests that <c>GetSubjectId</c> reads only the <c>sub</c> claim and returns it unchanged.</summary>
[Trait(TestTraitNames.CategoryName, SharedKernelTestTraitNames.UnitScope)]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_or_blank_subjects_never_fall_back_to_display_names_or_other_identifiers(string? subject)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "display-name"), new(ClaimTypes.NameIdentifier, "different-id"),
            new(ClaimTypes.Email, "staff@example.test")
        };
        if (subject is not null)
        {
            claims.Add(new Claim(ApiAuthenticationDefaults.SubjectClaimType, subject));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        var actorId = principal.GetSubjectId();

        // Assert
        actorId.ShouldBeNull();
    }

    [Fact]
    public void The_opaque_subject_is_preserved_without_parsing_or_normalization()
    {
        // Arrange
        const string subject = "keycloak-user:ABC-123";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.SubjectClaimType, subject)], "test"));

        // Act
        var actorId = principal.GetSubjectId();

        // Assert
        actorId.ShouldBe(subject);
        ApiAuthenticationDefaults.SubjectClaimType.ShouldBe("sub");
    }

    [Fact]
    public void A_missing_principal_is_rejected_explicitly()
    {
        // Act
        Action read = () => ClaimsPrincipalExtensions.GetSubjectId(null!);

        // Assert
        read.ShouldThrow<ArgumentNullException>();
    }
}
