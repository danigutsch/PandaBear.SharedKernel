using System.Reflection;
using Microsoft.Playwright;

namespace PandaBear.Testing.Tests;

// DispatchProxy derives a page type from this class at run time, so the class can be neither sealed nor abstract. The
// page holds no resources and fails on every member, so one shared instance serves every test.
public class TitleAssertionPageProxy : DispatchProxy
{
    public static IPage Page { get; } = Create<IPage, TitleAssertionPageProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException("The title assertion must validate its arguments before it uses the page.");
}
