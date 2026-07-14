using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Verifies application-owned permission policy requirements.
/// </summary>
public sealed class AuthorizationPolicyBuilderExtensionsTests
{
    [Fact]
    public void Requires_the_application_permission_claim()
    {
        // Arrange
        var builder = new AuthorizationPolicyBuilder();

        // Act
        var result = builder.RequirePermission("tours.read");
        var policy = result.Build();

        // Assert
        policy.Requirements.ShouldHaveSingleItem();
        policy.Requirements.OfType<ClaimsAuthorizationRequirement>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_blank_permissions(string permission)
    {
        // Arrange
        var builder = new AuthorizationPolicyBuilder();

        // Act
        Action action = () => builder.RequirePermission(permission);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }
}
