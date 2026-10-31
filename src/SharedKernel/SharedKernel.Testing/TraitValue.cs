using System.Text;

namespace PandaBear.Testing;

/// <summary>Converts enum members to the kebab-case values used by trait attributes.</summary>
internal static class TraitValue
{
    /// <summary>Converts a defined enum member name such as <c>UiIntegration</c> to its kebab-case trait value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not a defined member of <typeparamref name="TEnum"/>.</exception>
    internal static string Of<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Undefined {typeof(TEnum).Name}.");
        }

        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 2);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }
}
