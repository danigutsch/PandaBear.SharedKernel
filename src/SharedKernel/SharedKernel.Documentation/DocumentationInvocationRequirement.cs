namespace PandaBear.Documentation;

/// <summary>Requirement that a method be invoked a given number of times with given arguments in a C# source file.</summary>
internal sealed class DocumentationInvocationRequirement
{
    /// <summary>Repository-relative C# file that is searched for invocations.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Method whose body is searched; empty searches top-level statements.</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Parameter count that selects the overload of the searched method.</summary>
    public int ParameterCount
    {
        get; set;
    }

    /// <summary>Name of the invoked method to count.</summary>
    public string InvokedMethodName { get; set; } = string.Empty;

    /// <summary>Number of invocations of the invoked method that must be found.</summary>
    public int ExpectedCount { get; set; } = 1;

    /// <summary>Arguments the invocation must pass, in order; empty skips the argument check.</summary>
    public List<string> ExpectedArguments { get; set; } = [];
}
