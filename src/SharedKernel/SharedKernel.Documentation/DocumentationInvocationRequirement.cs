namespace SharedKernel.Documentation;

internal sealed class DocumentationInvocationRequirement
{
    public string SourcePath { get; set; } = string.Empty;

    public string MethodName { get; set; } = string.Empty;

    public int ParameterCount { get; set; }

    public string InvokedMethodName { get; set; } = string.Empty;

    public int ExpectedCount { get; set; } = 1;

    public List<string> ExpectedArguments { get; set; } = [];
}
