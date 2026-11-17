namespace Aspire.Hosting;

/// <summary>The Umami request that setting up a website failed at.</summary>
internal enum UmamiProvisioningStep
{
    /// <summary><c>POST /api/auth/login</c>.</summary>
    SignIn,

    /// <summary><c>GET /api/websites/{id}</c>.</summary>
    Lookup,

    /// <summary><c>POST /api/websites</c>.</summary>
    Create,
}
