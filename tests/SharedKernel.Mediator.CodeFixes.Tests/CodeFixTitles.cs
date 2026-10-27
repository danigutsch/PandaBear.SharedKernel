namespace PandaBear.Mediator.CodeFixes.Tests;

/// <summary>
/// Well-known code action titles produced by the mediator code-fix provider.
/// </summary>
internal static class CodeFixTitles
{
    /// <summary>Title of the action that appends a CancellationToken ct parameter to a handler method.</summary>
    public const string AddCancellationTokenParameter = "Add CancellationToken ct parameter";
    /// <summary>Title of the action that puts [EnumeratorCancellation] on a stream handler's token parameter.</summary>
    public const string AddEnumeratorCancellation = "Add [EnumeratorCancellation]";
    /// <summary>Title of the action that adds an assembly-level MediatorModule attribute.</summary>
    public const string AddMediatorModuleAttribute = "Add [assembly: MediatorModule]";
    /// <summary>Title of the action that adds a public Handle method to a handler type.</summary>
    public const string AddPublicHandleMethod = "Add public Handle method";
    /// <summary>Title of the action that raises a non-public type to public.</summary>
    public const string MakeTypePublic = "Make type public";

    /// <summary>Builds the title of the action that passes the enclosing method's token, named by paramName, to a mediator call.</summary>
    public static string ForwardCancellationToken(string paramName) =>
        $"Forward CancellationToken {paramName}";

    /// <summary>Builds the title of the action that adds the request interface named by interfaceDisplayName to a type.</summary>
    public static string AddRequestInterface(string interfaceDisplayName) =>
        $"Add {interfaceDisplayName}";
}
