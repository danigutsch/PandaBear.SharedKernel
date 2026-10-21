namespace PandaBear.BuildingBlocks;

internal static class BrazilianCheckDigits
{
    internal static int Calculate(ReadOnlySpan<char> value, int initialWeight)
    {
        var sum = 0;
        var weight = initialWeight;
        foreach (var character in value)
        {
            sum += (character - '0') * weight;
            weight = weight == 2 ? 9 : weight - 1;
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
