namespace PandaBear.Blazor.Tests;

/// <summary>Hostile strings that must never reach free-form user messages unchanged.</summary>
internal static class HostileText
{
    /// <summary>Script and image markup.</summary>
    public const string Markup = "<script>alert('x')</script><img src=x onerror=alert(1)>";

    /// <summary>A single-line stack-trace-like message.</summary>
    public const string StackTrace = "System.InvalidOperationException: secret at Contoso.Orders.Save(Order order) in /src/Orders.cs:line 42";

    /// <summary>A multi-line stack-trace-like message.</summary>
    public const string MultilineStackTrace = "Boom\n   at Contoso.Orders.Save(Order order)\n   at Contoso.Program.Main()";

    /// <summary>Creates a string of the given length.</summary>
    /// <param name="length">The length.</param>
    /// <returns>A string of <c>x</c> characters.</returns>
    public static string VeryLong(int length) => new('x', length);
}
