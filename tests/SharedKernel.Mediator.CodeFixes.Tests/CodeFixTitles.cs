namespace SharedKernel.Mediator.CodeFixes.Tests;

/// <summary>
/// Well-known code action titles produced by the mediator code-fix provider.
/// </summary>
internal static class CodeFixTitles
{
    public const string AddCancellationTokenParameter = "Add CancellationToken ct parameter";
    public const string AddEnumeratorCancellation = "Add [EnumeratorCancellation]";
    public const string AddMediatorModuleAttribute = "Add [assembly: MediatorModule]";
    public const string AddPublicHandleMethod = "Add public Handle method";
    public const string MakeTypePublic = "Make type public";

    public static string ForwardCancellationToken(string paramName) =>
        $"Forward CancellationToken {paramName}";

    public static string AddRequestInterface(string interfaceDisplayName) =>
        $"Add {interfaceDisplayName}";
}
