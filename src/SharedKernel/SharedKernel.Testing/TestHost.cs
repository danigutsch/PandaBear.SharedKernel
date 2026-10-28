namespace PandaBear.Testing;

/// <summary>
/// How a test is hosted. The value of the <c>Host</c> trait.
/// </summary>
public enum TestHost
{
    /// <summary>No host; the code under test runs directly in the test process.</summary>
    InProcess,

    /// <summary>An in-memory ASP.NET Core test server.</summary>
    TestServer,

    /// <summary>An Aspire application host with real resources.</summary>
    Aspire,

    /// <summary>A real browser driven by the test.</summary>
    Browser,
}
