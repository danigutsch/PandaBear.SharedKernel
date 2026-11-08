namespace PandaBear.BuildingBlocks;

/// <summary>Check digit arithmetic shared by the Brazilian CPF and CNPJ value objects.</summary>
internal static class BrazilianCheckDigits
{
    /// <summary>Computes one modulo-11 check digit from the digits in <paramref name="value" />, weighting from <paramref name="initialWeight" /> down to 2 and wrapping back to 9.</summary>
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
