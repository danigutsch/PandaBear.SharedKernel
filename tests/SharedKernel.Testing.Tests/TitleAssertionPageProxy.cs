using System.Reflection;
using Microsoft.Playwright;

namespace PandaBear.Testing.Tests;

// DispatchProxy derives a page type from this class at run time, so the class can be neither sealed nor abstract. The
// page holds no resources and fails on every member, so one shared instance serves every test.
/// <summary>Dispatch proxy that stands in for an <see cref="IPage" /> and throws on every member call, proving the title assertion validates arguments before touching the page.</summary>
public class TitleAssertionPageProxy : DispatchProxy
{
    /// <summary>Gets the shared proxied page, on which any member call throws <see cref="InvalidOperationException" />.</summary>
    public static IPage Page { get; } = Create<IPage, TitleAssertionPageProxy>();

    /// <summary>Throws <see cref="InvalidOperationException" /> for every intercepted call.</summary>
    /// <param name="targetMethod">The page member being invoked.</param>
    /// <param name="args">The arguments of the call.</param>
    /// <returns>Never returns.</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException("The title assertion must validate its arguments before it uses the page.");
}
