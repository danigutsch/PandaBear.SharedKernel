using System.Text.Json.Serialization;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>Writes and reads an enum of a <see cref="ThemeDocument" /> by its member name, such as <c>Pill</c>; a number is refused.</summary>
/// <typeparam name="TEnum">The enum.</typeparam>
internal sealed class NamedEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Creates the converter: member names as declared, no numbers.</summary>
    public NamedEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
