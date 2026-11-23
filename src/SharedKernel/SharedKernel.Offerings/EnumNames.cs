using System.Globalization;

namespace PandaBear.Offerings;

/// <summary>Names enum values for field paths.</summary>
internal static class EnumNames
{
    /// <summary>Gets the camelCase name of a value (<c>ElevationGain</c> gives <c>elevationGain</c>), or its number when it is not defined.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    public static string CamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(value);
        return string.IsNullOrEmpty(name)
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }
}
