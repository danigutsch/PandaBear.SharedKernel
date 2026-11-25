namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>One request the fake identity provider received.</summary>
/// <param name="Uri">The endpoint called.</param>
/// <param name="Authorization">The Authorization header, if any.</param>
/// <param name="Form">The form fields of the body.</param>
internal sealed record IdentityProviderRequest(Uri Uri, string? Authorization, IReadOnlyDictionary<string, string> Form);
