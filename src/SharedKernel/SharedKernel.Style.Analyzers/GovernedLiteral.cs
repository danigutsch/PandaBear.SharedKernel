namespace PandaBear.Style.Analyzers;

internal sealed class GovernedLiteral(string value, string assembly, string symbol, string? justification)
{
    public string Value { get; } = value;
    public string Assembly { get; } = assembly;
    public string Symbol { get; } = symbol;
    public string? Justification { get; } = justification;
}
