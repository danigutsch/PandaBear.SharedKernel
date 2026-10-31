namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Luhn check plus brand prefix and length ranges for payment card numbers.</summary>
internal static class CardNumberValidator
{
    /// <summary>Whether digits form a Luhn-valid number with a known brand prefix and length.</summary>
    /// <param name="digits">ASCII digits only.</param>
    /// <param name="brand">Brand name when valid.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="digits"/> is <see langword="null"/>.</exception>
    public static bool TryGetBrand(string digits, out string? brand)
    {
        ArgumentNullException.ThrowIfNull(digits);
        brand = null;
        if (digits.Length is < 12 or > 19 || !digits.All(char.IsAsciiDigit) || !IsLuhnValid(digits))
        {
            return false;
        }

        brand = Brand(digits);
        return brand is not null;
    }

    /// <summary>Luhn mod 10 check.</summary>
    /// <param name="digits">ASCII digits only.</param>
    /// <returns><see langword="true"/> when the check digit matches.</returns>
    public static bool IsLuhnValid(ReadOnlySpan<char> digits)
    {
        int sum = 0;
        bool doubleIt = false;
        for (int index = digits.Length - 1; index >= 0; index--)
        {
            int digit = digits[index] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    private static string? Brand(string digits)
    {
        int length = digits.Length;
        int two = Prefix(digits, 2);
        int three = Prefix(digits, 3);
        int four = Prefix(digits, 4);

        // Maestro and Discover prefixes overlap broader ranges, so the narrow ones are checked first.
        if (four is 5018 or 5020 or 5038 or 5893 or 6304 or 6759 or (>= 6761 and <= 6763) && length is >= 12 and <= 19)
        {
            return "Maestro";
        }

        if (digits[0] == '4' && length is 13 or 16 or 19)
        {
            return "Visa";
        }

        if (((two is >= 51 and <= 55) || four is >= 2221 and <= 2720) && length == 16)
        {
            return "Mastercard";
        }

        if (two is 34 or 37 && length == 15)
        {
            return "Amex";
        }

        if ((four == 6011 || three is >= 644 and <= 649 || two == 65) && length is >= 16 and <= 19)
        {
            return "Discover";
        }

        if (four is >= 3528 and <= 3589 && length is >= 16 and <= 19)
        {
            return "JCB";
        }

        if (two is 30 or 36 or 38 or 39 && length is >= 14 and <= 19)
        {
            return "Diners Club";
        }

        if (two == 62 && length is >= 16 and <= 19)
        {
            return "UnionPay";
        }

        return null;
    }

    private static int Prefix(string digits, int count)
    {
        int value = 0;
        for (int index = 0; index < count; index++)
        {
            value = (value * 10) + (digits[index] - '0');
        }

        return value;
    }
}
