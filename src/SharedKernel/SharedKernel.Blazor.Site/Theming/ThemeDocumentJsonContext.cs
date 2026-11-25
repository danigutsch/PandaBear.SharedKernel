using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// Source-generated JSON metadata of <see cref="ThemeDocument" />: camel-case properties read case-insensitively, enums by member name
/// (never as numbers), and a <see langword="null" /> value left out. A member the format does not define is ignored, as it always was.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters =
    [
        typeof(NamedEnumConverter<ControlShape>),
        typeof(NamedEnumConverter<GenericFontFamily>),
        typeof(NamedEnumConverter<ThemeColorRole>),
        typeof(NamedEnumConverter<ThemeFontStyle>),
        typeof(NamedEnumConverter<ThemeTextTransform>),
    ])]
[JsonSerializable(typeof(ThemeDocument))]
internal sealed partial class ThemeDocumentJsonContext : JsonSerializerContext;
