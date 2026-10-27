namespace PandaBear.Documentation;

/// <summary>Locates a method, or the top-level statements, in a C# source file.</summary>
internal sealed class DocumentationSourceMethod
{
    /// <summary>Repository-relative C# file that holds the method.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Method to read; empty reads top-level statements.</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Parameter count that selects the overload of the method.</summary>
    public int ParameterCount
    {
        get; set;
    }
}
